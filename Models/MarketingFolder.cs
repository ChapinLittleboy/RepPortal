namespace RepPortal.Models;

public class MarketingFolder
{
    public int Id { get; set; }
    public string DisplayName { get; set; }
    public string FolderRelativePath { get; set; }

    // Populated at runtime, not in the DB
    public List<MarketingFile> Files { get; set; } = new List<MarketingFile>();
}

public class MarketingFile
{
    public string Name { get; set; }
    public string Url { get; set; }
    public string SizeText { get; set; }
}