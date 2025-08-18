namespace Synapse.PatientDmeNeedsUtility
{
    /// <summary>
    /// Configuration settings for LLM-based note processing.
    /// </summary>
    public class LlmSettings
    {
        public string OpenRouterApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/chat/completions";
        public string Model { get; set; } = "deepseek/deepseek-chat-v3-0324:free";
        public double Temperature { get; set; } = 0.0;
        public bool IsEnabled { get; set; } = true;
    }
}