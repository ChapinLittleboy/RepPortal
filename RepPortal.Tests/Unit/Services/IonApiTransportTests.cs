using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using RepPortal.Models;
using RepPortal.Services;

namespace RepPortal.Tests.Unit.Services;

public class IonTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_SendsExpectedCredentialsAndCachesToken()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            HttpStatusCode.OK, "{\"access_token\":\"token-1\",\"expires_in\":3600}"));
        var provider = CreateTokenProvider(handler);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal("token-1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("https://auth.example.com/token.oauth2", handler.LastRequest!.Url);
        Assert.Equal("Basic", handler.LastRequest.Authorization?.Scheme);
        Assert.Contains("grant_type=password", handler.LastRequest.Body);
        Assert.Contains("username=service-key", handler.LastRequest.Body);
        Assert.Contains("password=service-secret", handler.LastRequest.Body);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ConcurrentCallersShareOneAuthenticationRequest()
    {
        var handler = new RecordingHandler(async _ =>
        {
            await Task.Delay(25);
            return JsonResponse(
                HttpStatusCode.OK, "{\"access_token\":\"shared\",\"expires_in\":3600}");
        });
        var provider = CreateTokenProvider(handler);

        var tokens = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => provider.GetAccessTokenAsync()));

        Assert.All(tokens, token => Assert.Equal("shared", token));
        Assert.Equal(1, handler.CallCount);
    }

    private static IonTokenProvider CreateTokenProvider(RecordingHandler handler)
    {
        var client = new HttpClient(handler);
        return new IonTokenProvider(
            new StubHttpClientFactory(client),
            Options.Create(CreateOptions()));
    }

    internal static CsiOptions CreateOptions() => new()
    {
        UseApi = true,
        IonApiUrl = "https://ion.example.com",
        TenantId = "TRN_TENANT",
        OAuthBaseUrl = "https://auth.example.com",
        OAuthTokenPath = "token.oauth2",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        ServiceAccountKey = "service-key",
        ServiceAccountSecret = "service-secret",
        Configurations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BAT"] = "TRN BAT",
            ["KENT"] = "TRN KENT"
        }
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json) };
}

public class CsiRestClientTests
{
    [Fact]
    public async Task GetAsync_RoutesThroughIonAndSelectsMongooseConfiguration()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"MessageCode\":0}") });
        var tokenProvider = new StubTokenProvider("bearer-token");
        var client = new CsiRestClient(
            new HttpClient(handler), tokenProvider,
            Options.Create(IonTokenProviderTests.CreateOptions()));

        await client.GetAsync("json/SLCustomers/adv", new Dictionary<string, string>
        {
            ["filter"] = "CustNum = '123'"
        }, "KENT");

        var request = handler.LastRequest!;
        Assert.StartsWith(
            "https://ion.example.com/TRN_TENANT/CSI/IDORequestService/MGRestService.svc/json/SLCustomers/adv?",
            request.Url);
        Assert.Contains("filter=CustNum%20%3D%20%27123%27", request.Url);
        Assert.Equal("Bearer", request.Authorization?.Scheme);
        Assert.Equal("bearer-token", request.Authorization?.Parameter);
        Assert.Equal("TRN KENT", request.MongooseConfiguration);
    }

    [Fact]
    public async Task GetAsync_Unauthorized_InvalidatesTokenAndRetriesExactlyOnce()
    {
        var handler = new RecordingHandler(call => call == 1
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var tokenProvider = new StubTokenProvider("first", "second");
        var client = new CsiRestClient(
            new HttpClient(handler), tokenProvider,
            Options.Create(IonTokenProviderTests.CreateOptions()));

        var result = await client.GetAsync("json/SLItems/adv");

        Assert.Equal("{}", result);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(1, tokenProvider.InvalidationCount);
        Assert.Equal(2, tokenProvider.GetTokenCount);
    }
}

public class CsiOptionsValidatorTests
{
    [Fact]
    public void Validate_AllowsMissingIonSettingsWhenApiIsDisabled()
    {
        var result = new CsiOptionsValidator().Validate(null, new CsiOptions { UseApi = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ReportsMissingIonSettingsWhenApiIsEnabled()
    {
        var result = new CsiOptionsValidator().Validate(null, new CsiOptions { UseApi = true });

        Assert.True(result.Failed);
        Assert.Contains("IonApiUrl", result.FailureMessage);
        Assert.Contains("Configurations:BAT", result.FailureMessage);
    }
}

internal sealed class StubTokenProvider(params string[] tokens) : IIonTokenProvider
{
    private int _tokenIndex;
    public int GetTokenCount { get; private set; }
    public int InvalidationCount { get; private set; }

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        GetTokenCount++;
        var index = Math.Min(_tokenIndex++, tokens.Length - 1);
        return Task.FromResult(tokens[index]);
    }

    public void Invalidate() => InvalidationCount++;
}

internal sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal sealed record RecordedRequest(
    string Url,
    AuthenticationHeaderValue? Authorization,
    string? MongooseConfiguration,
    string Body);

internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Func<int, Task<HttpResponseMessage>> _responseFactory;
    private int _callCount;

    public RecordingHandler(Func<int, HttpResponseMessage> responseFactory)
        : this(call => Task.FromResult(responseFactory(call)))
    {
    }

    public RecordingHandler(Func<int, Task<HttpResponseMessage>> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public int CallCount => _callCount;
    public RecordedRequest? LastRequest { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref _callCount);
        var body = request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken);
        LastRequest = new RecordedRequest(
            request.RequestUri!.OriginalString,
            request.Headers.Authorization,
            request.Headers.TryGetValues("X-Infor-MongooseConfig", out var values)
                ? values.FirstOrDefault()
                : null,
            body);
        return await _responseFactory(call);
    }
}
