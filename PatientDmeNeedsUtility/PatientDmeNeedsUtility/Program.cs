using Microsoft.Extensions.Configuration;
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
        static async Task<int> Main(string[] args)
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
                // Set up configuration
                const string appSettingsJsonFileName = "appsettings.json";
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(appSettingsJsonFileName, optional: false, reloadOnChange: true)
                    .Build();

                var appSettings = new AppSettings();
                configuration.Bind(appSettings);

                // Parse the physician note to extract DME needs and serialize to JSON
                var physicianNoteText = resourceHelper.ReadResourceFile(appSettings.Files.DefaultPhysicianNoteFile);
                logger.LogDebug("Parsing physician note");
                var result = noteParser.Parse(physicianNoteText);
                string serializedJson = JsonConvert.SerializeObject(result, Formatting.Indented);
                logger.LogDebug("Parsed note into JSON: {Json}", serializedJson);

                // Parse the physician note to extract DME needs and serialize to JSON
                using var httpClient = new HttpClient();
                var client = new ApiClient(httpClient, loggerFactory.CreateLogger<ApiClient>(), appSettings.Api.BaseRetryMs, appSettings.Api.MaxRetries);
                var request = new HttpRequestMessage(HttpMethod.Post, appSettings.Api.FullUrl)
                {
                    Content = new StringContent(serializedJson, Encoding.UTF8, "application/json")
                };

                logger.LogDebug("Posting JSON: {Json}", serializedJson);
                var response = await client.SendWithRetryAsync(request);

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
