namespace RepPortal.Models;

public class PortalFolder
{
    public int Id { get; set; }
    public string PageType { get; set; } = ""; // Pricing, Forms, Marketing
    public string DisplayName { get; set; } = "";
    public string FolderRelativePath { get; set; } = "";
    public int DisplayOrder { get; set; }
    public List<PortalFile> Files { get; set; } = new();
}
