namespace RepPortal.Models;

public class PackingListItem
{
    public int CoLine { get; set; }
    public string Item { get; set; } = "";
    public string ItemDesc { get; set; } = "";
    public string UM { get; set; } = "";
    public string ShipmentId { get; set; } = "";
    public decimal QtyPicked { get; set; }
    public decimal QtyShipped { get; set; }
}
