namespace RepPortal.Services;

using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

public class CsiLoggingHandler : DelegatingHandler
{
    private readonly ILogger<CsiLoggingHandler> _logger;

    public CsiLoggingHandler(ILogger<CsiLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // ---- REQUEST LOGGING ----
        _logger.LogInformation(
            "CSI REQUEST {Method} {Url}",
            request.Method,
            request.RequestUri);

        if (request.RequestUri?.Query is { Length: > 0 })
        {
            _logger.LogDebug("CSI QUERY {Query}", request.RequestUri.Query);
        }

        // Never log auth token
        var safeHeaders = request.Headers
            .Where(h => h.Key != "Authorization")
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value));

        _logger.LogDebug("CSI HEADERS {@Headers}", safeHeaders);

        // ---- CALL ----
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSI REQUEST FAILED");
            throw;
        }

        stopwatch.Stop();

        // ---- RESPONSE LOGGING ----
        _logger.LogInformation(
            "CSI RESPONSE {StatusCode} in {ElapsedMs}ms",
            (int)response.StatusCode,
            stopwatch.ElapsedMilliseconds);

        var content = await response.Content.ReadAsStringAsync();

        // MGRest returns MessageCode/Message even on 200
        _logger.LogDebug("CSI RESPONSE BODY {Body}", Truncate(content, 4000));

        return response;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength) + "…(truncated)";
}

