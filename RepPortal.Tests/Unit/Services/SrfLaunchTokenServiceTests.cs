using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using RepPortal.Models;
using RepPortal.Services;

namespace RepPortal.Tests.Unit.Services;

public sealed class SrfLaunchTokenServiceTests
{
    private const string Secret =
        "test-only-srf-launch-secret-that-is-at-least-32-bytes";

    [Fact]
    public void TryCreateToken_CreatesVerifiableTokenWithExpectedExpiration()
    {
        var options = Options.Create(new SrfLaunchOptions
        {
            LaunchSecret = Secret,
            TokenLifetimeSeconds = 60
        });
        var service = new SrfLaunchTokenService(options);
        var now = DateTimeOffset.FromUnixTimeSeconds(1_786_986_000);

        var succeeded = service.TryCreateToken(now, out var token, out var error);

        Assert.True(succeeded, error);
        var parts = token.Split('.');
        Assert.Equal(4, parts.Length);
        Assert.Equal("v1", parts[0]);
        Assert.Equal(now.AddSeconds(60).ToUnixTimeSeconds().ToString(), parts[1]);
        Assert.Equal(16, WebEncoders.Base64UrlDecode(parts[2]).Length);

        var payload = string.Join('.', parts.Take(3));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var expectedSignature = WebEncoders.Base64UrlEncode(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        Assert.Equal(expectedSignature, parts[3]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    public void TryCreateToken_RejectsMissingOrShortConfiguredSecret(string? secret)
    {
        var options = Options.Create(new SrfLaunchOptions
        {
            LaunchSecret = secret,
            TokenLifetimeSeconds = 60
        });
        var service = new SrfLaunchTokenService(options);

        var succeeded = service.TryCreateToken(
            DateTimeOffset.UtcNow,
            out var token,
            out var error);

        Assert.False(succeeded);
        Assert.Empty(token);
        Assert.Contains("32 bytes", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void TryCreateToken_RejectsLifetimeOutsideAllowedRange(int lifetimeSeconds)
    {
        var options = Options.Create(new SrfLaunchOptions
        {
            LaunchSecret = Secret,
            TokenLifetimeSeconds = lifetimeSeconds
        });
        var service = new SrfLaunchTokenService(options);

        var succeeded = service.TryCreateToken(
            DateTimeOffset.UtcNow,
            out _,
            out var error);

        Assert.False(succeeded);
        Assert.Contains("between 1 and 300 seconds", error);
    }
}
