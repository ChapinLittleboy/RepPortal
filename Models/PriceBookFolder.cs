namespace RepPortal.Models;

public class PriceBookFolder
{
    public int Id { get; set; }
    public string DisplayName { get; set; }
    public string FolderRelativePath { get; set; }

    // Populated at runtime, not in the DB
    public List<PriceBookFile> Files { get; set; } = new List<PriceBookFile>();
}

public class PriceBookFile
{
    public string Name { get; set; }
    public string Url { get; set; }
    public string SizeText { get; set; }
}