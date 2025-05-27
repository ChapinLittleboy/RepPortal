namespace RepPortal.Models;

public class ModelContext
{
    public string User { get; set; }
    public string Role { get; set; }
    public string Page { get; set; }
    public Dictionary<string, string> DataFilters { get; set; } = new();
    public string Summary { get; set; }
}
