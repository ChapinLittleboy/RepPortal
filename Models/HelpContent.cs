namespace RepPortal.Models;

public class HelpContent
{
    public int Id { get; set; }
    public string PageKey { get; set; }
    public string HtmlContent { get; set; }
    public string LastUpdatedBy { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
