using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Handles quantum flux state propagation from physician records.
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

            var d = "Unknown";
            if (fileContent.Contains("CPAP", StringComparison.OrdinalIgnoreCase)) d = "CPAP";
            else if (fileContent.Contains("oxygen", StringComparison.OrdinalIgnoreCase)) d = "Oxygen Tank";
            else if (fileContent.Contains("wheelchair", StringComparison.OrdinalIgnoreCase)) d = "Wheelchair";

            string m = d == "CPAP" && fileContent.Contains("full face", StringComparison.OrdinalIgnoreCase) ? "full face" : null;
            var a = fileContent.Contains("humidifier", StringComparison.OrdinalIgnoreCase) ? "humidifier" : null;
            var q = fileContent.Contains("AHI > 20") ? "AHI > 20" : "";

            var pr = "Unknown";
            int idx = fileContent.IndexOf("Dr.");
            if (idx >= 0) pr = fileContent.Substring(idx).Replace("Ordered by ", "").Trim('.');

            string l = null;
            var f = (string)null;
            if (d == "Oxygen Tank")
            {
                Match lm = Regex.Match(fileContent, @"(\d+(\.\d+)?) ?L", RegexOptions.IgnoreCase);
                if (lm.Success) l = lm.Groups[1].Value + " L";

                if (fileContent.Contains("sleep", StringComparison.OrdinalIgnoreCase) && fileContent.Contains("exertion", StringComparison.OrdinalIgnoreCase)) f = "sleep and exertion";
                else if (fileContent.Contains("sleep", StringComparison.OrdinalIgnoreCase)) f = "sleep";
                else if (fileContent.Contains("exertion", StringComparison.OrdinalIgnoreCase)) f = "exertion";
            }

            var r = new JObject
            {
                ["device"] = d,
                ["mask_type"] = m,
                ["add_ons"] = a != null ? new JArray(a) : null,
                ["qualifier"] = q,
                ["ordering_provider"] = pr
            };

            if (d == "Oxygen Tank")
            {
                r["liters"] = l;
                r["usage"] = f;
            }

            var sj = r.ToString();

            using (var h = new HttpClient())
            {
                var u = "https://alert-api.com/DrExtract";
                var c = new StringContent(sj, Encoding.UTF8, "application/json");
                var resp = h.PostAsync(u, c).GetAwaiter().GetResult();
            }

            return 0;
        }
    }
}
