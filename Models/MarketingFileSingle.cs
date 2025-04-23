namespace RepPortal.Models;

public class MarketingFileSingle
{
    
    public int Id { get; set; }
    public string DisplayName { get; set; }
    public string FolderRelativePath { get; set; }
    public string FileName { get; set; }
    public int DisplayOrder { get; set; }


    // Populated at runtime, not in the DB
    public List<MarketingFileSingle> Files { get; set; } = new List<MarketingFileSingle>();
}


