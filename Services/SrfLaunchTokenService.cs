using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using RepPortal.Models;

namespace RepPortal.Services;

public interface ISrfLaunchTokenService
{
    bool TryCreateToken(DateTimeOffset now, out string token, out string error);
}

public sealed class SrfLaunchTokenService : ISrfLaunchTokenService
{
    private const int MinimumSecretBytes = 32;
    private const int NonceBytes = 16;
    private readonly SrfLaunchOptions _options;

    public SrfLaunchTokenService(IOptions<SrfLaunchOptions> options)
    {
        _options = options.Value;
    }

    public bool TryCreateToken(DateTimeOffset now, out string token, out string error)
    {
        token = string.Empty;
        error = string.Empty;

        var secret = _options.LaunchSecret;

        if (string.IsNullOrWhiteSpace(secret) ||
            Encoding.UTF8.GetByteCount(secret) < MinimumSecretBytes)
        {
            error = "The SRF launch secret is missing or shorter than 32 bytes.";
            return false;
        }

        if (_options.TokenLifetimeSeconds is < 1 or > 300)
        {
            error = "The SRF token lifetime must be between 1 and 300 seconds.";
            return false;
        }

        var expires = now
            .AddSeconds(_options.TokenLifetimeSeconds)
            .ToUnixTimeSeconds();
        var nonce = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(NonceBytes));
        var payload = $"v1.{expires}.{nonce}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = WebEncoders.Base64UrlEncode(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        token = $"{payload}.{signature}";
        return true;
    }
}
