using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Processes physician notes to extract medical device needs (CPAP, oxygen tanks, wheelchairs)
    /// and related specifications, then sends the extracted data to an API endpoint.
    /// </summary>
    internal sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddSimpleConsole(options =>
                    {
                        options.TimestampFormat = "hh:mm:ss ";
                        options.SingleLine = true;
                        options.IncludeScopes = true;
                    });
            });

            var logger = loggerFactory.CreateLogger<Program>();
            logger.LogInformation("Starting Patient DME Needs Utility");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var appSettings = new AppSettings();
            configuration.Bind(appSettings);

            var llmSettings = new LlmSettings();
            configuration.GetSection("Llm").Bind(llmSettings);

            var resourceHelper = new ResourceFileHelper(loggerFactory.CreateLogger<ResourceFileHelper>());

            try
            {
                // Read physician note from resource
                var noteText = resourceHelper.ReadResourceFile(appSettings.Files.DefaultPhysicianNoteFile);

                // Get the expected result as well
                var expectedResult = resourceHelper.ReadResourceFile(appSettings.Files.DefaultExpectedOutputFile);

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", llmSettings.OpenRouterApiKey);
                var apiClient = new ApiClient(httpClient, loggerFactory.CreateLogger<ApiClient>(),
                                              appSettings.Api.BaseRetryMs, appSettings.Api.MaxRetries);

                var parser = new PhysicianNoteParser(loggerFactory.CreateLogger<PhysicianNoteParser>(),
                                                     apiClient, llmSettings);

                // If LLM is enabled, parse physician note (LLM first, fallback to manual)
                // Otherwise, fall back to manually parsing the note.
                PatientDmeNeeds patientNeeds = llmSettings.IsEnabled
                    ? await ParseNoteWithFallbackAsync(parser, noteText, expectedResult, logger)
                    : parser.Parse(noteText);

                string json = JsonConvert.SerializeObject(patientNeeds, Formatting.Indented);
                logger.LogDebug("Parsed note JSON: {Json}", json);

                // Send to API
                await PostJsonToApiAsync(apiClient, appSettings.Api.FullUrl, json, logger);

                logger.LogInformation("Processing completed successfully");
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during processing");
                return 1;
            }
        }

        private static async Task<PatientDmeNeeds> ParseNoteWithFallbackAsync(
            PhysicianNoteParser parser, string noteText,
            string expectedResult, ILogger logger)
        {
            try
            {
                logger.LogDebug("Parsing physician note via LLM...");
                return await parser.ParseWithLlm(noteText, expectedResult);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LLM parsing failed, falling back to manual parsing.");
                return parser.Parse(noteText);
            }
        }

        private static async Task PostJsonToApiAsync(ApiClient client, string url, string json, ILogger logger)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            logger.LogDebug("Posting JSON to API: {Json}", json);
            var response = await client.SendWithRetryAsync(request);
            logger.LogInformation("API responded with: {StatusCode}", response.StatusCode);
        }
    }
}
