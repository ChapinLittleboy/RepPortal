using System.Text.Json;
using Microsoft.Extensions.Options;
using RepPortal.Models;

namespace RepPortal.Services;

public interface ICsiRestClient
{
    Task<string> GetAsync(string relativeUrl, IDictionary<string, string>? query = null);
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

    public async Task<string> GetAsync(
        string relativeUrl,
        IDictionary<string, string>? query = null)
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