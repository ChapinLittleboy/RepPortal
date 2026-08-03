using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using RepPortal.Models;

namespace RepPortal.Services;

public interface ICsiRestClient
{
    Task<string> GetAsync(
        string relativeUrl,
        IDictionary<string, string>? query = null,
        string site = "BAT",
        CancellationToken cancellationToken = default);
}

public sealed class CsiRestClient : ICsiRestClient
{
    private readonly HttpClient _httpClient;
    private readonly IIonTokenProvider _tokenProvider;
    private readonly CsiOptions _options;

    public CsiRestClient(
        HttpClient httpClient,
        IIonTokenProvider tokenProvider,
        IOptions<CsiOptions> options)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _options = options.Value;
    }

    public async Task<string> GetAsync(
        string relativeUrl,
        IDictionary<string, string>? query = null,
        string site = "BAT",
        CancellationToken cancellationToken = default)
    {
        if (!_options.Configurations.TryGetValue(site, out var mongooseConfiguration)
            || string.IsNullOrWhiteSpace(mongooseConfiguration))
        {
            throw new InvalidOperationException(
                $"No CSI Mongoose configuration is configured for site '{site}'.");
        }

        var url = BuildUrl(relativeUrl, query);
        var retriedUnauthorized = false;
        var retriedThrottled = false;

        while (true)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("X-Infor-MongooseConfig", mongooseConfiguration);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && !retriedUnauthorized)
            {
                retriedUnauthorized = true;
                _tokenProvider.Invalidate();
                continue;
            }

            if ((int)response.StatusCode == 429 && !retriedThrottled)
            {
                retriedThrottled = true;
                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
                await Task.Delay(delay > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : delay,
                    cancellationToken);
                continue;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (LooksLikeHtml(content))
                throw new InvalidOperationException(
                    $"ION Gateway returned HTML instead of an MGRest response. URL: {url}");

            ThrowForUnsuccessfulStatus(response, url);
            return content;
        }
    }

    private string BuildUrl(string relativeUrl, IDictionary<string, string>? query)
    {
        var baseUrl =
            $"{_options.IonApiUrl.TrimEnd('/')}/{_options.TenantId.Trim('/')}/CSI/IDORequestService/MGRestService.svc";
        var url = $"{baseUrl}/{relativeUrl.TrimStart('/')}";

        if (query is not { Count: > 0 })
            return url;

        var queryString = string.Join("&", query.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value.Trim())}"));
        return $"{url}?{queryString}";
    }

    private static bool LooksLikeHtml(string content) =>
        content.TrimStart().StartsWith("<", StringComparison.OrdinalIgnoreCase);

    private static void ThrowForUnsuccessfulStatus(HttpResponseMessage response, string url)
    {
        if (response.IsSuccessStatusCode)
            return;

        var status = (int)response.StatusCode;
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "ION authentication was rejected after refreshing the access token.",
            HttpStatusCode.Forbidden =>
                "ION denied the request. Verify the ION API suite and Mongoose permissions.",
            HttpStatusCode.NotFound =>
                "ION could not find the gateway route or requested custom IDO.",
            _ when status == 429 =>
                "ION throttled the request after one bounded retry.",
            _ when status >= 500 =>
                "ION Gateway or the SyteLine service returned a server error.",
            _ => $"ION API request failed with HTTP {status} {response.ReasonPhrase}."
        };

        throw new HttpRequestException($"{message} URL: {url}", null, response.StatusCode);
    }
}

public class MgRestResponse<T>
{
    public string MessageCode { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Bookmark { get; set; }
    public List<T> Items { get; set; } = [];
}

public class MgNameValue
{
    public string Name { get; set; } = "";
    public string? Value { get; set; }
}

public class MgRestAdvResponse
{
    public string? Bookmark { get; set; }
    public List<List<MgNameValue>> Items { get; set; } = [];
    public string Message { get; set; } = "";
    public int MessageCode { get; set; }
}
