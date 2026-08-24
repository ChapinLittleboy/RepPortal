using Microsoft.Extensions.Options;

namespace RepPortal.Models;

public sealed class CsiOptionsValidator : IValidateOptions<CsiOptions>
{
    public ValidateOptionsResult Validate(string? name, CsiOptions options)
    {
        if (!options.UseApi)
            return ValidateOptionsResult.Success;

        var missing = new List<string>();
        AddIfBlank(options.IonApiUrl, "IonApiUrl", missing);
        AddIfBlank(options.TenantId, "TenantId", missing);
        AddIfBlank(options.OAuthBaseUrl, "OAuthBaseUrl", missing);
        AddIfBlank(options.OAuthTokenPath, "OAuthTokenPath", missing);
        AddIfBlank(options.ClientId, "ClientId", missing);
        AddIfBlank(options.ClientSecret, "ClientSecret", missing);
        AddIfBlank(options.ServiceAccountKey, "ServiceAccountKey", missing);
        AddIfBlank(options.ServiceAccountSecret, "ServiceAccountSecret", missing);

        if (options.Configurations is null
            || !options.Configurations.TryGetValue("BAT", out var batConfiguration)
            || string.IsNullOrWhiteSpace(batConfiguration))
        {
            missing.Add("Configurations:BAT");
        }

        if (missing.Count > 0)
            return ValidateOptionsResult.Fail(
                $"CSI ION configuration is incomplete. Missing: {string.Join(", ", missing)}.");

        if (!Uri.TryCreate(options.IonApiUrl, UriKind.Absolute, out _))
            return ValidateOptionsResult.Fail("CSI:IonApiUrl must be an absolute URL.");

        if (!Uri.TryCreate(options.OAuthBaseUrl, UriKind.Absolute, out _))
            return ValidateOptionsResult.Fail("CSI:OAuthBaseUrl must be an absolute URL.");

        return ValidateOptionsResult.Success;
    }

    private static void AddIfBlank(string value, string name, ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value))
            missing.Add(name);
    }
}
