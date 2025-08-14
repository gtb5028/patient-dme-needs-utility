using System.Text;

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

            var result = PhysicianNoteParser.Parse(fileContent);
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
