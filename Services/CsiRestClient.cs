using System.Text.Json;
using Microsoft.Extensions.Options;
using RepPortal.Models;

namespace RepPortal.Services;

public interface ICsiRestClient
{
    Task<string> GetAsync(string relativeUrl, IDictionary<string, string>? query = null);
    Task<string> GetAsync(string relativeUrl, IDictionary<string, string>? query, string authorizationOverride);
}

public class CsiRestClient : ICsiRestClient
{
    private readonly HttpClient _httpClient;

    public CsiRestClient(HttpClient httpClient, IOptions<CsiOptions> options)
    {
        _httpClient = httpClient;

        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);

        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "Authorization",
            options.Value.Authorization);
    }

    public Task<string> GetAsync(
        string relativeUrl,
        IDictionary<string, string>? query = null)
        => GetAsyncCore(relativeUrl, query, authorizationOverride: null);

    public Task<string> GetAsync(
        string relativeUrl,
        IDictionary<string, string>? query,
        string authorizationOverride)
        => GetAsyncCore(relativeUrl, query, authorizationOverride);

    private async Task<string> GetAsyncCore(
        string relativeUrl,
        IDictionary<string, string>? query,
        string? authorizationOverride)
    {
        var baseUrl = _httpClient.BaseAddress!.ToString().TrimEnd('/');
        var relUrl = relativeUrl.TrimStart('/');

        var url = $"{baseUrl}/{relUrl}";

        if (query is { Count: > 0 })
        {
            var qs = string.Join("&",
                query.Select(kvp =>
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value.Trim())}"));

            url = $"{url}?{qs}";
        }

        if (!string.IsNullOrWhiteSpace(authorizationOverride))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", authorizationOverride);
            using var overrideResponse = await _httpClient.SendAsync(request);
            var overrideContent = await overrideResponse.Content.ReadAsStringAsync();

            if (overrideContent.StartsWith("<", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"MGRest returned HTML. URL: {url}");

            overrideResponse.EnsureSuccessStatusCode();
            return overrideContent;
        }

        using var response = await _httpClient.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (content.StartsWith("<", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"MGRest returned HTML. URL: {url}");

        response.EnsureSuccessStatusCode();

        return content;
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