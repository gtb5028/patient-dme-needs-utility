namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Configuration settings for the Patient DME Needs Utility application.
    /// Contains API endpoints, file paths, parsing patterns, and other configurable values.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// API configuration settings.
        /// </summary>
        public ApiSettings Api { get; set; } = new ApiSettings();

        /// <summary>
        /// File path configuration settings.
        /// </summary>
        public FileSettings Files { get; set; } = new FileSettings();
    }

    /// <summary>
    /// API endpoint and communication settings.
    /// </summary>
    public class ApiSettings
    {
        /// <summary>
        /// The base URL for the DME extraction API.
        /// </summary>
        public string BaseUrl { get; set; } = "https://alert-api.com";

        /// <summary>
        /// The endpoint path for DME data extraction.
        /// </summary>
        public string ExtractEndpoint { get; set; } = "/DrExtract";

        /// <summary>
        /// Base retry in MS for API calls.
        /// </summary>
        public int BaseRetryMs { get; set; } = 1000;

        /// <summary>
        /// Max retries for API calls.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets the full API URL by combining base URL and endpoint.
        /// </summary>
        public string FullUrl => $"{BaseUrl.TrimEnd('/')}{ExtractEndpoint}";
    }

    /// <summary>
    /// File path and resource settings.
    /// </summary>
    public class FileSettings
    {
        /// <summary>
        /// Default physician note file to process.
        /// </summary>
        public string DefaultPhysicianNoteFile { get; set; } = "physician_note1.txt";
    }
}