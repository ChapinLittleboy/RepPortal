namespace RepPortal.Models;


public class RegionItem
{
    public string Region { get; set; }
    public string RegionName { get; set; }

    public string DisplayText => $"{Region} - {RegionName}";
}
