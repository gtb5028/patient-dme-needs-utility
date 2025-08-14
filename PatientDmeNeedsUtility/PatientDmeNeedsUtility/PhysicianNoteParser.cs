using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Provides functionality to parse physician notes and extract Durable Medical Equipment (DME) needs,
    /// including device types, specifications, and ordering provider information.
    /// </summary>
    public static class PhysicianNoteParser
    {
        /// <summary>
        /// Parses physician note content and extracts relevant DME order details.
        /// </summary>
        /// <param name="physicianNoteText">The text content of the physician note to parse.</param>
        public static JObject Parse(string physicianNoteText)
        {
            var deviceType = "Unknown";
            if (physicianNoteText.Contains("CPAP", StringComparison.OrdinalIgnoreCase))
            {
                deviceType = "CPAP";
            }
            else if (physicianNoteText.Contains("oxygen", StringComparison.OrdinalIgnoreCase))
            {
                deviceType = "Oxygen Tank";
            }
            else if (physicianNoteText.Contains("wheelchair", StringComparison.OrdinalIgnoreCase))
            {
                deviceType = "Wheelchair";
            }

            string maskType = null;
            if (deviceType == "CPAP" && physicianNoteText.Contains("full face", StringComparison.OrdinalIgnoreCase))
            {
                maskType = "full face";
            }

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
            var usage = (string)null;
            if (deviceType == "Oxygen Tank")
            {
                Match literMatch = Regex.Match(physicianNoteText, @"(\d+(\.\d+)?) ?L", RegexOptions.IgnoreCase);
                if (literMatch.Success)
                {
                    liters = literMatch.Groups[1].Value + " L";
                }

                if (physicianNoteText.Contains("sleep", StringComparison.OrdinalIgnoreCase) &&
                    physicianNoteText.Contains("exertion", StringComparison.OrdinalIgnoreCase))
                {
                    usage = "sleep and exertion";
                }
                else if (physicianNoteText.Contains("sleep", StringComparison.OrdinalIgnoreCase))
                {
                    usage = "sleep";
                }
                else if (physicianNoteText.Contains("exertion", StringComparison.OrdinalIgnoreCase))
                {
                    usage = "exertion";
                }
            }

            var result = new JObject
            {
                ["device"] = deviceType,
                ["mask_type"] = maskType,
                ["add_ons"] = addOns != null ? new JArray(addOns) : null,
                ["qualifier"] = qualifier,
                ["ordering_provider"] = orderingProvider
            };

            if (deviceType == "Oxygen Tank")
            {
                result["liters"] = liters;
                result["usage"] = usage;
            }

            return result;
        }
    }
}
