using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
            // Set up logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Debug) // show Debug+ logs
                    .AddSimpleConsole(options =>
                    {
                        options.TimestampFormat = "hh:mm:ss ";
                        options.SingleLine = true;
                        options.IncludeScopes = true;
                    });
            });

            var logger = loggerFactory.CreateLogger<Program>();
            logger.LogInformation("Starting DME Needs Utility");

            // Create instance of ResourceFileHelper with injected logger
            var resourceHelper = new ResourceFileHelper(loggerFactory.CreateLogger<ResourceFileHelper>());
            var noteParser = new PhysicianNoteParser(loggerFactory.CreateLogger<PhysicianNoteParser>());

            try
            {
                // Load the physician note from file
                const string fileName = "physician_note1.txt";
                var physicianNoteText = resourceHelper.ReadResourceFile(fileName);

                // Parse the physician note to extract DME needs and serialize to JSON
                logger.LogDebug("Parsing physician note");
                var result = noteParser.Parse(physicianNoteText);
                string serializedJson = JsonConvert.SerializeObject(result, Formatting.Indented);
                logger.LogDebug("Parsed note into JSON: {Json}", serializedJson);

                using (var httpClient = new HttpClient())
                {
                    var apiUrl = "https://alert-api.com/DrExtract";
                    logger.LogInformation("Sending data to API: {ApiUrl}", apiUrl);

                    var content = new StringContent(serializedJson, Encoding.UTF8, "application/json");
                    var response = httpClient.PostAsync(apiUrl, content).GetAwaiter().GetResult();
                    logger.LogInformation("API response status: {StatusCode}", response.StatusCode);
                }

                logger.LogInformation("Processing completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during processing");
                return 1;
            }
        }
    }
}
