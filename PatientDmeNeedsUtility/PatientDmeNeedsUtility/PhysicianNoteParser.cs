using Microsoft.Extensions.Logging;
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

        public const string AhiQualifierKeyword = "AHI > 20";
        public const string HumidifierKeyword = "humidifier";
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

        public PhysicianNoteParser(ILogger<PhysicianNoteParser> logger)
        {
            _logger = logger;
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
                MedicalDeviceType deviceType = ParseDeviceType(physicianNoteText);
                _logger.LogInformation("Identified device type: {DeviceType}", deviceType);

                MaskType maskType = ParseMaskType(physicianNoteText, deviceType);
                if (maskType != MaskType.None)
                {
                    _logger.LogDebug("Detected mask type: {MaskType}", maskType);
                }

                string addOns = ParseAddOns(physicianNoteText);
                if (!string.IsNullOrEmpty(addOns))
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

        /// <summary>
        /// Parses the physician note text to identify any DME add-ons mentioned.
        /// </summary>
        /// <param name="physicianNoteText">The text content of the physician note to parse.</param>
        public string ParseAddOns(string physicianNoteText)
        {
            bool hasAddOn = physicianNoteText.Contains(HumidifierKeyword, StringComparison.OrdinalIgnoreCase);
            _logger.LogDebug("Add-on detection: {HasAddOn}", hasAddOn);
            return hasAddOn ? HumidifierKeyword : string.Empty;
        }

        /// <summary>
        /// Extracts the ordering provider's name from physician notes.
        /// Handles multiple label formats and cleans trailing punctuation.
        /// </summary>
        /// <param name="physicianNoteText">The physician note text to parse.</param>
        public string ParseOrderingProvider(string physicianNoteText)
        {
            var providerPattern = @"(?:Ordering Physician|Ordered By)\s*:?\s*(.+)";
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
            var match = Regex.Match(physicianNoteText, @"Patient Name:\s*(.+)", RegexOptions.IgnoreCase);

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
            var match = Regex.Match(physicianNoteText, @"Diagnosis:\s*(.+)", RegexOptions.IgnoreCase);

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
    }
}