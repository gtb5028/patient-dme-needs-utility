using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Processes physician notes to extract medical device orders (CPAP, oxygen tanks, wheelchairs)
    /// and related specifications, then sends the extracted data to an API endpoint.
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            // Load the physician note from file
            var fileName = "physician_note1.txt";
            string fileContent;

            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, fileName);

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"File not found: {fileName}", path);
                }

                fileContent = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    throw new InvalidDataException($"File '{fileName}' is empty or contains only whitespace.");
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to load file '{fileName}'.", ex);
            }

            var deviceType = "Unknown";
            if (fileContent.Contains("CPAP", StringComparison.OrdinalIgnoreCase))
            {
                deviceType = "CPAP";
            }
            else if (fileContent.Contains("oxygen", StringComparison.OrdinalIgnoreCase))
            {
                deviceType = "Oxygen Tank";
            }
            else if (fileContent.Contains("wheelchair", StringComparison.OrdinalIgnoreCase))
            {
                deviceType = "Wheelchair";
            }

            string maskType = null;
            if (deviceType == "CPAP" && fileContent.Contains("full face", StringComparison.OrdinalIgnoreCase))
            {
                maskType = "full face";
            }

            var addOns = fileContent.Contains("humidifier", StringComparison.OrdinalIgnoreCase)
                ? "humidifier"
                : null;

            var qualifier = fileContent.Contains("AHI > 20")
                ? "AHI > 20"
                : "";

            var orderingProvider = "Unknown";
            int providerNameIndex = fileContent.IndexOf("Dr.");
            if (providerNameIndex >= 0)
            {
                orderingProvider = fileContent.Substring(providerNameIndex)
                    .Replace("Ordered by ", "")
                    .Trim('.');
            }

            string liters = null;
            var usage = (string)null;
            if (deviceType == "Oxygen Tank")
            {
                Match literMatch = Regex.Match(fileContent, @"(\d+(\.\d+)?) ?L", RegexOptions.IgnoreCase);
                if (literMatch.Success)
                {
                    liters = literMatch.Groups[1].Value + " L";
                }

                if (fileContent.Contains("sleep", StringComparison.OrdinalIgnoreCase) &&
                    fileContent.Contains("exertion", StringComparison.OrdinalIgnoreCase))
                {
                    usage = "sleep and exertion";
                }
                else if (fileContent.Contains("sleep", StringComparison.OrdinalIgnoreCase))
                {
                    usage = "sleep";
                }
                else if (fileContent.Contains("exertion", StringComparison.OrdinalIgnoreCase))
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

            var serializedJson = result.ToString();

            using (var httpClient = new HttpClient())
            {
                var apiUrl = "https://alert-api.com/DrExtract";
                var content = new StringContent(serializedJson, Encoding.UTF8, "application/json");
                var response = httpClient.PostAsync(apiUrl, content).GetAwaiter().GetResult();
            }

            return 0;
        }
    }
}
