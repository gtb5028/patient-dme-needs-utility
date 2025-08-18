using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Sockets;

/// <summary>
/// Provides resilient HTTP request handling with automatic retry logic for transient failures.
/// This client intelligently differentiates between permanent errors (DNS failures, invalid URLs) 
/// and transient failures (timeouts, server errors), applying exponential backoff for retryable cases.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private const int MaxRetries = 3;
    private const int BaseRetryMs = 1000;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var exceptions = new List<Exception>();

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);

                if ((int)response.StatusCode >= 500) // Server errors
                    throw new HttpRequestException($"Server error: {response.StatusCode}");

                return response;
            }
            catch (Exception ex) when (ShouldRetry(ex) && attempt < MaxRetries)
            {
                exceptions.Add(ex);
                var delay = BaseRetryMs * (int)Math.Pow(2, attempt - 1);
                _logger.LogWarning(ex, $"Attempt {attempt} failed. Retrying in {delay}ms...");
                await Task.Delay(delay, cancellationToken);
            }
        }
        throw new AggregateException("Max retries reached", exceptions);
    }

    private bool ShouldRetry(Exception ex)
    {
        // Never retry these
        if (ex is OperationCanceledException ||
            ex is UriFormatException ||
            ex is JsonException)
            return false;

        // Network/DNS failures
        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.InnerException is SocketException sockEx)
            {
                return sockEx.SocketErrorCode switch
                {
                    SocketError.HostNotFound => false,
                    SocketError.HostUnreachable => false,
                    SocketError.ConnectionRefused => true, // Host may come up
                    _ => true
                };
            }
            return true; // Other HTTP errors
        }

        // Timeouts and transient errors
        return ex is TimeoutException;
    }
}