namespace RepPortal.Models;

public class InvoiceRptDetail
{
    public string Slsman { get; set; }
    public string Cust { get; set; }
    public int CustSeq { get; set; }
    public string B2Name { get; set; }
    public string Name { get; set; }
    public string State { get; set; }
    public string Site { get; set; }
    public string CoNum { get; set; }
    public string CustPO { get; set; }
    public string Item { get; set; }
    public decimal InvQty { get; set; }
    public decimal? OrdQty { get; set; }  // Nullable in case not all rows have an order quantity
    public decimal Price { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? OrdDate { get; set; }
    public DateTime InvDate { get; set; }
    public string InvNum { get; set; }
    public decimal ExtPrice { get; set; }

    // Computed property for Cust_Num
    public string Cust_Num => Cust.PadLeft(7);
}
