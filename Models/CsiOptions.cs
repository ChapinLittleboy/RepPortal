using Microsoft.Extensions.Options;

namespace RepPortal.Models;

public class CsiOptions
{
    public string IonApiUrl { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string OAuthBaseUrl { get; set; } = "";
    public string OAuthTokenPath { get; set; } = "token.oauth2";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string ServiceAccountKey { get; set; } = "";
    public string ServiceAccountSecret { get; set; } = "";
    public Dictionary<string, string> Configurations { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public bool UseApi { get; set; }
    public DateTime? OpenOrderCutoffDate { get; set; }


}
