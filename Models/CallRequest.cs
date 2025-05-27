using System.Text.Json;

namespace RepPortal.Models;

public class CallRequest
{
    public string Method { get; set; } = "";
    public Dictionary<string, JsonElement> Parameters { get; set; } = new();
}

