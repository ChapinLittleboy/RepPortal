namespace RepPortal.Models;

public class PackingListHeader
{
    public string PackNum { get; set; } = "";
    public DateTime? PackDate { get; set; }
    public string Whse { get; set; } = "";
    public string CoNum { get; set; } = "";
    public string CustNum { get; set; } = "";
    public string ShipCode { get; set; } = "";
    public string Carrier { get; set; } = "";
    public string ShipAddr { get; set; } = "";
    public string ShipAddr2 { get; set; } = "";
    public string ShipAddr3 { get; set; } = "";
    public string ShipAddr4 { get; set; } = "";
    public string ShipCity { get; set; } = "";
    public string ShipState { get; set; } = "";
    public string ShipZip { get; set; } = "";
    public string CustPo { get; set; } = "";
}
