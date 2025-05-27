namespace RepPortal.Models;

public class PortalFile
{
    public int Id { get; set; }
    public int FolderId { get; set; }
    public string DisplayName { get; set; } = "";
    public string FileName { get; set; } = "";
    public int DisplayOrder { get; set; }
}