namespace RepPortal.Models;

public class InvoiceRptParameters
{
    public DateTime BeginInvoiceDate { get; set; }
    public DateTime EndInvoiceDate { get; set; }
    public string RepCode { get; set; }
    public string CustNum { get; set; }
    public string CorpNum { get; set; }
    public string CustType { get; set; }
    public string EndUserType { get; set; }
    public List<string> AllowedRegions { get; set; } = new List<string>();

}