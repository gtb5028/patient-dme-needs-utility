using System.Text.RegularExpressions;

namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Provides functionality to parse physician notes and extract Durable Medical Equipment (DME) needs,
    /// including device types, specifications, and ordering provider information.
    /// </summary>
    public static class PhysicianNoteParser
    {
        public static readonly Dictionary<string, MedicalDeviceType> DeviceKeywords =
            new Dictionary<string, MedicalDeviceType>(StringComparer.OrdinalIgnoreCase)
            {
                { "CPAP", MedicalDeviceType.CPAP },
                { "oxygen", MedicalDeviceType.OxygenTank },
                { "wheelchair", MedicalDeviceType.Wheelchair }
            };

        /// <summary>
        /// Parses physician note content and extracts relevant DME order details.
        /// </summary>
        /// <param name="physicianNoteText">The text content of the physician note to parse.</param>
        public static PatientDmeNeeds Parse(string physicianNoteText)
        {
            if (string.IsNullOrWhiteSpace(physicianNoteText))
            {
                throw new ArgumentException(nameof(physicianNoteText), "Physician note text cannot be null or whitespace.");
            }

            MedicalDeviceType deviceType = ParseDeviceType(physicianNoteText);
            MaskType maskType = ParseMaskType(physicianNoteText, deviceType);

            var addOns = physicianNoteText.Contains("humidifier", StringComparison.OrdinalIgnoreCase)
                ? "humidifier"
                : null;

            var qualifier = physicianNoteText.Contains("AHI > 20")
                ? "AHI > 20"
                : "";

            var providerPattern = @"(?:Ordering Physician|Ordered By)\s*:?\s*(.+)";
            var matchProvider = Regex.Match(physicianNoteText, providerPattern, RegexOptions.IgnoreCase);
            var orderingProvider = matchProvider.Success
                ? matchProvider.Groups[1].Value.Trim().TrimEnd('.', ',')
                : "Unknown";

            string liters = null;
            var usage = ParseUsage(physicianNoteText);
            if (deviceType == MedicalDeviceType.OxygenTank)
            {
                Match literMatch = Regex.Match(physicianNoteText, @"(\d+(\.\d+)?) ?L", RegexOptions.IgnoreCase);
                if (literMatch.Success)
                {
                    liters = literMatch.Groups[1].Value + " L";
                }
            }

            var matchName = Regex.Match(physicianNoteText, @"Patient Name:\s*(.+)", RegexOptions.IgnoreCase);
            string patientName = matchName.Success ? matchName.Groups[1].Value.Trim() : string.Empty;

            var matchDob = Regex.Match(physicianNoteText, @"DOB:\s*(\d{1,2}/\d{1,2}/\d{4})", RegexOptions.IgnoreCase);
            string dob = matchDob.Success ? matchDob.Groups[1].Value : string.Empty;

            var matchDiagnosis = Regex.Match(physicianNoteText, @"Diagnosis:\s*(.+)", RegexOptions.IgnoreCase);
            var diagnosis = matchDiagnosis.Success ? matchDiagnosis.Groups[1].Value.Trim() : string.Empty;

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
                Qualifier = qualifier
            };

            return result;
        }

        /// <summary>
        /// Parses the physician note text to detect which medical device is mentioned.
        /// </summary>
        /// <param name="physicianNoteText">The text content of the physician note to parse.</param>
        public static MedicalDeviceType ParseDeviceType(string physicianNoteText)
        {
            foreach (var (keyword, deviceType) in DeviceKeywords)
            {
                if (physicianNoteText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return deviceType;
                }
            }

            return MedicalDeviceType.Unknown;
        }

        /// <summary>
        /// Determines the mask type required based on physician notes and detected device type.
        /// </summary>
        /// <param name="physicianNoteText">The physician's note text to analyze.</param>
        /// <param name="currentDeviceType">The medical device type detected from the notes.</param>
        public static MaskType ParseMaskType(string physicianNoteText, MedicalDeviceType currentDeviceType)
        {
            if (currentDeviceType == MedicalDeviceType.CPAP &&
                physicianNoteText.Contains("full face", StringComparison.OrdinalIgnoreCase))
            {
                return MaskType.FullFace;
            }

            return MaskType.None;
        }

        /// <summary>
        /// Parses physician note content and extracts usage.
        /// </summary>
        /// <param name="physicianNoteText">The text content of the physician note to parse.</param>
        public static HashSet<Usage> ParseUsage(string physicianNoteText)
        {
            var usage = new HashSet<Usage>();

            if (string.IsNullOrWhiteSpace(physicianNoteText))
            {
                return usage;
            }

            if (physicianNoteText.Contains("sleep", StringComparison.OrdinalIgnoreCase))
            {
                usage.Add(Usage.Sleep);
            }

            if (physicianNoteText.Contains("exertion", StringComparison.OrdinalIgnoreCase))
            {
                usage.Add(Usage.Exertion);
            }

            return usage;
        }
    }
}