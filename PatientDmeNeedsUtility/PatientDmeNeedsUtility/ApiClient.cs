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
    private readonly int _maxRetries = 3;
    private readonly int _baseRetryMs = 1000;

    public ApiClient(
        HttpClient httpClient,
        ILogger<ApiClient> logger,
        int maxRetries = 3,
        int baseRetryMs = 1000)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxRetries = maxRetries;
        _baseRetryMs = baseRetryMs;
    }

    public async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var exceptions = new List<Exception>();

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);

                if ((int)response.StatusCode >= 500) // Server errors
                    throw new HttpRequestException($"Server error: {response.StatusCode}");

                return response;
            }
            catch (Exception ex) when (ShouldRetry(ex) && attempt < _maxRetries)
            {
                exceptions.Add(ex);
                var delay = _baseRetryMs * (int)Math.Pow(2, attempt - 1);
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