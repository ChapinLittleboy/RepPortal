using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RepPortal.Models;

namespace RepPortal.Services;

public interface IIonTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    void Invalidate();
}

public sealed class IonTokenProvider : IIonTokenProvider, IDisposable
{
    public const string HttpClientName = "IonOAuth";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CsiOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public IonTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<CsiOptions> options,
        TimeProvider? timeProvider = null)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (TokenIsCurrent())
            return _accessToken!;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (TokenIsCurrent())
                return _accessToken!;

            await AuthenticateAsync(cancellationToken);
            return _accessToken!;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate()
    {
        _accessToken = null;
        _expiresAt = DateTimeOffset.MinValue;
    }

    private bool TokenIsCurrent() =>
        !string.IsNullOrWhiteSpace(_accessToken) && _timeProvider.GetUtcNow() < _expiresAt;

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        var tokenUrl = $"{_options.OAuthBaseUrl.TrimEnd('/')}/{_options.OAuthTokenPath.TrimStart('/')}";
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{_options.ClientId}:{_options.ClientSecret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = _options.ServiceAccountKey,
            ["password"] = _options.ServiceAccountSecret
        });

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"ION authentication failed ({(int)response.StatusCode} {response.ReasonPhrase}).");

        using var document = JsonDocument.Parse(payload);
        _accessToken = document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidDataException("ION authentication did not return an access token.");
        var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiresElement)
            && expiresElement.TryGetInt32(out var seconds)
                ? seconds
                : 3600;
        _expiresAt = _timeProvider.GetUtcNow().AddSeconds(Math.Max(1, expiresIn - 60));
    }

    public void Dispose() => _refreshLock.Dispose();
}
