namespace RepPortal.Models;

public class InvoiceRptDetail
{
    public string Slsman { get; set; }
    public string Cust { get; set; }
    public int CustSeq { get; set; }
    public string B2Name { get; set; }
    public string Name { get; set; }
    public string State { get; set; }
    [CsiField("SiteRef")] public string Site { get; set; }
    [CsiField("CoNum")] public string CoNum { get; set; }
    public string CustPO { get; set; }
    [CsiField("Item")] public string Item { get; set; }
    [CsiField("QtyInvoiced")] public decimal InvQty { get; set; }
    public decimal? OrdQty { get; set; }  // Nullable in case not all rows have an order quantity
    [CsiField("Price")] public decimal Price { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? OrdDate { get; set; }
    public DateTime InvDate { get; set; }
    [CsiField("InvNum")] public string InvNum { get; set; }
    public decimal ExtPrice { get; set; }
    public DateTime? Ship_Date { get; set; }
    public string? ShipToRegion { get; set; }

    // Computed property for Cust_Num
    public string Cust_Num => Cust.PadLeft(7);
}
