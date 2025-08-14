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
            const string fileName = "physician_note1.txt";
            var physicianNoteText = ResourceFileHelper.ReadResourceFile(fileName);

            // Parse the physician note to extract DME needs and serialize to JSON
            var result = PhysicianNoteParser.Parse(physicianNoteText);
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
