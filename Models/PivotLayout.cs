namespace RepPortal.Models;

public class PivotLayout
{
    public int Id { get; set; }
    public string RepCode { get; set; } = "";
    public string PageKey { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string Report { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
