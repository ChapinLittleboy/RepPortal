namespace RepPortal.Models;

public class FormsDownloadFolder
{
    public int Id { get; set; }
    public string DisplayName { get; set; }
    public string FolderRelativePath { get; set; }

    // Populated at runtime, not in the DB
    public List<FormsDownloadFile> Files { get; set; } = new List<FormsDownloadFile>();
}

public class FormsDownloadFile
{
    public string Name { get; set; }
    public string Url { get; set; }
    public string SizeText { get; set; }
}