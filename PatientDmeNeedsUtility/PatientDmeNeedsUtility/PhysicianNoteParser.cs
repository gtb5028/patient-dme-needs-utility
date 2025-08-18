using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Provides functionality to parse physician notes and extract Durable Medical Equipment (DME) needs,
    /// including device types, specifications, and ordering provider information.
    /// </summary>
    public class PhysicianNoteParser
    {
        private readonly ILogger<PhysicianNoteParser> _logger;
        private readonly ApiClient _httpClient;
        private readonly LlmSettings _settings;

        public static readonly Dictionary<string, MedicalDeviceType> DeviceKeywords =
            new Dictionary<string, MedicalDeviceType>(StringComparer.OrdinalIgnoreCase)
            {
                { "CPAP", MedicalDeviceType.CPAP },
                { "oxygen", MedicalDeviceType.OxygenTank },
                { "wheelchair", MedicalDeviceType.Wheelchair },
                // Added devices here - simply map the keyword
                // to the enum.
                { "walker", MedicalDeviceType.Walker },
                { "cane", MedicalDeviceType.Cane },
                { "crutches", MedicalDeviceType.Crutches },
                { "nebulizer", MedicalDeviceType.Nebulizer },
                { "infusion pump", MedicalDeviceType.InfusionPump },
                { "tracheostomy tube", MedicalDeviceType.TracheostomyTube },
                { "prosthesis", MedicalDeviceType.Prosthesis },
                { "orthotic", MedicalDeviceType.Orthotic },
                { "blood glucose monitor", MedicalDeviceType.GlucoseMonitor },
                { "heart monitor", MedicalDeviceType.HeartMonitor }
            };

        public PhysicianNoteParser(ILogger<PhysicianNoteParser> logger, ApiClient httpClient, LlmSettings settings)
        {
            _logger = logger;
            _httpClient = httpClient;
            _settings = settings;
        }

        /// <summary>
        /// Parses physician note content and extracts relevant DME order details.
        /// </summary>
        /// <param name="physicianNoteText">The text content of the physician note to parse.</param>
        public PatientDmeNeeds Parse(string physicianNoteText)
        {
            _logger.LogDebug("Beginning parse of physician note");

            if (string.IsNullOrWhiteSpace(physicianNoteText))
            {
                _logger.LogError("Invalid input: physician note text was null or empty");
                throw new ArgumentException(nameof(physicianNoteText), "Physician note text cannot be null or whitespace.");
            }

            try
            {
                // Normalize the text to handle literal newlines in JSON
                physicianNoteText = physicianNoteText.Replace("\\n", "\n").Replace("\\r", "\r");

                MedicalDeviceType deviceType = ParseDeviceType(physicianNoteText);
                _logger.LogInformation("Identified device type: {DeviceType}", deviceType);

                MaskType maskType = ParseMaskType(physicianNoteText, deviceType);
                if (maskType != MaskType.None)
                {
                    _logger.LogDebug("Detected mask type: {MaskType}", maskType);
                }

                HashSet<string> addOns = ParseAddOns(physicianNoteText);
                if (addOns.Any())
                {
                    _logger.LogDebug("Found add-ons: {AddOns}", addOns);
                }

                HashSet<string> qualifiers = ParseQualifiers(physicianNoteText);
                if (qualifiers.Any())
                {
                    _logger.LogDebug("Identified qualifiers: {Qualifier}", string.Join(",", qualifiers));
                }

                string orderingProvider = ParseOrderingProvider(physicianNoteText);
                _logger.LogDebug("Extracted ordering provider: {Provider}", orderingProvider);

                string liters = ParseOxygenLiters(physicianNoteText, deviceType);
                if (!string.IsNullOrEmpty(liters))
                {
                    _logger.LogDebug("Determined oxygen flow rate: {Liters}", liters);
                }

                var usage = ParseUsage(physicianNoteText);
                if (usage.Count > 0)
                {
                    _logger.LogDebug("Identified usage scenarios: {Usage}", string.Join(", ", usage));
                }

                string patientName = ParsePatientName(physicianNoteText);
                _logger.LogDebug("Extracted patient name: {PatientName}", patientName);

                string dob = ParseDateOfBirth(physicianNoteText);
                _logger.LogDebug("Extracted date of birth: {DOB}", dob);

                string diagnosis = ParseDiagnosis(physicianNoteText);
                _logger.LogInformation("Identified diagnosis: {Diagnosis}", diagnosis);

                var result = new PatientDmeNeeds
                {
                    Device = deviceType,
                    Liters = liters,
                    Usage = usage,
                    Diagnosis = diagnosis,
                    OrderingProvider = orderingProvider,
                    PatientName = patientName,
                    DOB = dob,
                    MaskType = maskType,
                    AddOns = addOns,
                    Qualifiers = qualifiers
                };

                _logger.LogInformation("Successfully parsed physician note");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse physician note");
                throw;
            }
        }

        /// <summary>
        /// Parses a physician note represented as a JSON string into a <see cref="PatientDmeNeeds"/> object.
        /// </summary>
        /// <param name="jsonText">The JSON string representing a physician note.</param>
        public PatientDmeNeeds ParseJson(string jsonText)
        {
            try
            {
                string normalizedText = jsonText.Replace("\\n", "\n").Replace("\\r", "\r");
                JObject result = JObject.Parse(jsonText);
                var dmeNeeds = result.ToObject<PatientDmeNeeds>();

                if (dmeNeeds == null)
                {
                    _logger.LogWarning("Parsed JSON returned null PatientDmeNeeds object.");
                    return null;
                }

                return dmeNeeds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse physician note JSON");
                return null;
            }
        }

        public async Task<PatientDmeNeeds> ParseWithLlm(string noteText, string expectedJsonTemplate)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Processing physician note with LLM using model {Model}", _settings.Model);

                var prompt = BuildPrompt(noteText, expectedJsonTemplate);
                var response = await SendLlmRequest(prompt);
                var result = ParseResponse(response);

                stopwatch.Stop();
                _logger.LogInformation("Successfully processed note with LLM in {Duration}ms", stopwatch.ElapsedMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to process note with LLM after {Duration}ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        /// <summary>
        /// Parses the physician note text to detect which medical device is mentioned.
        /// </summary>
        /// <param name="physicianNoteText">The text content of the physician note to parse.</param>
        public MedicalDeviceType ParseDeviceType(string physicianNoteText)
        {
            _logger.LogDebug("Searching for device type in note text");

            foreach (var (keyword, deviceType) in DeviceKeywords)
            {
                if (physicianNoteText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Found device match: {Keyword} → {DeviceType}", keyword, deviceType);
                    return deviceType;
                }
            }

            _logger.LogWarning("No recognized device type found in note");
            return MedicalDeviceType.Unknown;
        }

        /// <summary>
        /// Determines the mask type required based on physician notes and detected device type.
        /// </summary>
        /// <param name="physicianNoteText">The physician's note text to analyze.</param>
        /// <param name="currentDeviceType">The medical device type detected from the notes.</param>
        public MaskType ParseMaskType(string physicianNoteText, MedicalDeviceType currentDeviceType)
        {
            if (currentDeviceType == MedicalDeviceType.CPAP &&
                physicianNoteText.Contains("full face", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Full face mask requirement detected");
                return MaskType.FullFace;
            }

            return MaskType.None;
        }

        public HashSet<string> ParseAddOns(string physicianNoteText)
        {
            var addOns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var knownAddOns = new[]
            {
                "humidifier",
                "heated tubing",
                "mask cushion",
                "headgear",
                "chin strap",
                "filter"
            };

            foreach (var addOn in knownAddOns)
            {
                if (physicianNoteText.Contains(addOn, StringComparison.OrdinalIgnoreCase))
                {
                    addOns.Add(addOn);
                    _logger.LogDebug("Detected add-on: {AddOn}", addOn);
                }
            }

            if (!addOns.Any())
            {
                _logger.LogDebug("No add-ons detected in note");
            }

            return addOns;
        }

        /// <summary>
        /// Extracts the ordering provider's name from physician notes.
        /// Handles multiple label formats and cleans trailing punctuation.
        /// </summary>
        /// <param name="physicianNoteText">The physician note text to parse.</param>
        public string ParseOrderingProvider(string physicianNoteText)
        {
            var providerPattern = @"(?:Ordering Physician|Ordered By)\s*:?\s*(.+?)(?:\r?\n|\\n|$)";
            var matchProvider = Regex.Match(physicianNoteText, providerPattern, RegexOptions.IgnoreCase);

            if (matchProvider.Success)
            {
                var provider = matchProvider.Groups[1].Value.Trim().TrimEnd('.', ',');
                _logger.LogDebug("Extracted provider name: {Provider}", provider);
                return provider;
            }

            _logger.LogWarning("No ordering provider found in note");
            return "Unknown";
        }

        /// <summary>
        /// Parses the physician note text for qualifying conditions that affect DME approval.
        /// Detects multiple qualifiers, including numeric AHI values for sleep apnea devices.
        /// </summary>
        /// <param name="physicianNoteText">The text content of the physician note to parse.</param>
        public HashSet<string> ParseQualifiers(string physicianNoteText)
        {
            var qualifiers = new HashSet<string>();

            // 1. Detect AHI with optional comparison (e.g., "AHI:28", "AHI=15", "AHI > 20")
            var ahiRegex = new Regex(@"\bAHI\s*([:=>]{1,2})\s*(\d+(\.\d+)?)\b", RegexOptions.IgnoreCase);
            var ahiMatches = ahiRegex.Matches(physicianNoteText);
            foreach (Match match in ahiMatches)
            {
                var op = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value;
                qualifiers.Add($"AHI{op}{value}");
                _logger.LogDebug("Detected AHI qualifier: {AHI}", $"AHI{op}{value}");
            }

            // 2. Detect other string-based qualifiers (example: portable, manual, foldable)
            var knownQualifiers = new[] { "portable", "manual", "foldable", "electric", "stationary" };
            foreach (var q in knownQualifiers)
            {
                if (physicianNoteText.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    qualifiers.Add(q);
                    _logger.LogDebug("Detected qualifier: {Qualifier}", q);
                }
            }

            if (!qualifiers.Any())
            {
                _logger.LogDebug("No qualifiers detected in note");
            }

            return qualifiers;
        }

        /// <summary>
        /// Parses and extracts the oxygen flow rate in liters from physician notes.
        /// Only processes notes when the prescribed device is an oxygen tank.
        /// </summary>
        /// <param name="physicianNoteText">The physician note text to parse.</param>
        /// <param name="deviceType">The detected medical device type.</param>
        public string ParseOxygenLiters(string physicianNoteText, MedicalDeviceType deviceType)
        {
            if (deviceType != MedicalDeviceType.OxygenTank)
            {
                _logger.LogDebug("Skipping liter flow parsing for non-oxygen device");
                return string.Empty;
            }

            const string literFlowPattern = @"(\d+(?:\.\d+)?)\s?L";
            var match = Regex.Match(physicianNoteText, literFlowPattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var liters = $"{match.Groups[1].Value} L";
                _logger.LogDebug("Extracted oxygen flow rate: {Liters}", liters);
                return liters;
            }

            _logger.LogWarning("No oxygen flow rate found for oxygen tank prescription");
            return string.Empty;
        }

        /// <summary>
        /// Extracts the patient name from physician notes when prefixed with "Patient Name:".
        /// Returns empty string if no match is found.
        /// </summary>
        /// <param name="physicianNoteText">The physician note text to parse.</param>
        public string ParsePatientName(string physicianNoteText)
        {
            var match = Regex.Match(physicianNoteText, @"Patient Name:\s*(.+?)(?:\r?\n|\\n|$)", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                _logger.LogDebug("Extracted patient name: {Name}", name);
                return name;
            }

            _logger.LogWarning("No patient name found in note");
            return string.Empty;
        }

        /// <summary>
        /// Extracts the patient's date of birth from physician notes when formatted as "DOB: MM/DD/YYYY".
        /// Returns empty string if no valid date format is found.
        /// </summary>
        /// <param name="physicianNoteText">The physician note text to parse.</param>
        public string ParseDateOfBirth(string physicianNoteText)
        {
            var match = Regex.Match(physicianNoteText, @"DOB:\s*(\d{1,2}/\d{1,2}/\d{4})", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var dob = match.Groups[1].Value;
                _logger.LogDebug("Extracted date of birth: {DOB}", dob);
                return dob;
            }

            _logger.LogWarning("No valid date of birth found in note");
            return string.Empty;
        }

        /// <summary>
        /// Extracts the diagnosis text from physician notes when prefixed with "Diagnosis:".
        /// Returns empty string if no diagnosis is found.
        /// </summary>
        /// <param name="physicianNoteText">The physician note text to parse.</param>
        public string ParseDiagnosis(string physicianNoteText)
        {
            var match = Regex.Match(physicianNoteText, @"Diagnosis:\s*(.+?)(?:\r?\n|\\n|$)", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var diagnosis = match.Groups[1].Value.Trim();
                _logger.LogDebug("Extracted diagnosis: {Diagnosis}", diagnosis);
                return diagnosis;
            }

            _logger.LogWarning("No diagnosis found in note");
            return string.Empty;
        }

        /// <summary>
        /// Parses physician note content and extracts usage.
        /// </summary>
        /// <param name="physicianNoteText">The text content of the physician note to parse.</param>
        public HashSet<Usage> ParseUsage(string physicianNoteText)
        {
            var usage = new HashSet<Usage>();
            _logger.LogDebug("Analyzing usage scenarios");

            if (string.IsNullOrWhiteSpace(physicianNoteText))
            {
                _logger.LogDebug("Empty note - no usage scenarios");
                return usage;
            }

            if (physicianNoteText.Contains("sleep", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Found sleep usage requirement");
                usage.Add(Usage.Sleep);
            }

            if (physicianNoteText.Contains("exertion", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Found exertion usage requirement");
                usage.Add(Usage.Exertion);
            }

            _logger.LogDebug("Identified {Count} usage scenarios", usage.Count);
            return usage;
        }

        private string BuildPrompt(string noteText, string expectedOutput)
        {
            var prompt = $@"
            You are a medical data extraction system designed to interpret physician notes and output structured medical device data.
            Your goal is to extract relevant fields and ensure that the values conform to the following enums:

            - Device types: {string.Join(", ", Enum.GetNames(typeof(MedicalDeviceType)))}
            - Mask types: {string.Join(", ", Enum.GetNames(typeof(MaskType)))}
            - Usage types: {string.Join(", ", Enum.GetNames(typeof(Usage)))}

            Return **only valid enum values** for these fields. If the note does not specify a value, use the default enum value (Unknown for Device, None for MaskType and Usage).

            Return your output in exactly this JSON format:
            {expectedOutput}

            Physician note:
            {noteText}

            Rules:
            1. Always match the enums exactly (case-insensitive is okay).
            2. Do not add extra fields.
            3. If multiple devices are mentioned, choose the primary one for this note.
            4. For any optional fields not mentioned in the note, use the enum default.
            ";

            return prompt;
        }

        private PatientDmeNeeds ParseResponse(string llmResponse)
        {
            if (string.IsNullOrWhiteSpace(llmResponse))
                throw new ArgumentException("LLM response cannot be null or empty", nameof(llmResponse));

            try
            {
                using var doc = JsonDocument.Parse(llmResponse);

                // Safely extract content with null checks
                if (!doc.RootElement.TryGetProperty("choices", out var choicesElement) ||
                    choicesElement.ValueKind != JsonValueKind.Array ||
                    choicesElement.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("Invalid LLM response: missing or empty 'choices' array");
                }

                var firstChoice = choicesElement[0];
                if (!firstChoice.TryGetProperty("message", out var messageElement) ||
                    !messageElement.TryGetProperty("content", out var contentElement))
                {
                    throw new InvalidOperationException("Invalid LLM response: missing message content");
                }

                var content = contentElement.GetString();
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("LLM response content is empty");
                }

                // Clean up potential code fences
                content = Regex.Replace(content, @"^```(json)?\s*|\s*```$", string.Empty, RegexOptions.Multiline).Trim();

                _logger.LogDebug("Parsing LLM response content: {Content}", content);

                var result = ParseJson(content);
                if (result == null)
                {
                    throw new InvalidOperationException("Failed to deserialize LLM response to PatientDmeNeeds");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse LLM response as JSON: {Response}", llmResponse);
                throw new InvalidOperationException("Invalid JSON in LLM response", ex);
            }
        }

        private async Task<string> SendLlmRequest(string prompt)
        {
            var requestBody = new
            {
                model = _settings.Model,
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant that outputs only valid JSON." },
                    new { role = "user", content = prompt }
                },
                temperature = _settings.Temperature
            };

            var jsonString = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
            var request = new HttpRequestMessage(HttpMethod.Post, _settings.BaseUrl)
            {
                Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendWithRetryAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"LLM API request failed with status {response.StatusCode}: {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}