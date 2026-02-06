using Microsoft.Extensions.Options;

namespace RepPortal.Models;

public class CsiOptions
{
    public string BaseUrl { get; set; } = "";
    public string Authorization { get; set; } = "";
    public string? MongooseConfig { get; set; }
    public bool UseApi { get; set; }
    public DateTime? OpenOrderCutoffDate { get; set; }


}