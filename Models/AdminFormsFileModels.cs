namespace RepPortal.Models;

public class AdminFormsFolder
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string FolderRelativePath { get; set; } = string.Empty;
}

public class AdminManagedFile
{
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string LastModifiedText { get; set; } = string.Empty;
}
