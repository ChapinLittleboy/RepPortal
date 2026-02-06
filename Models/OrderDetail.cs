
using Newtonsoft.Json;

namespace RepPortal.Models;

public class OrderDetail
{
    [CsiField("CoCustNum")]
    public string Cust { get; set; }

    [CsiField("Adr0Name")]
    public string CustName { get; set; }

    [CsiField("DerDueDate")]
    public DateTime DueDate { get; set; }

    [CsiField("CoOrderDate")]
    public DateTime OrdDate { get; set; }

    [CsiField("PromiseDate")]
    public DateTime? PromDate { get; set; }

    [CsiField("CoCustPo")]
    public string? CustPO { get; set; }

    [CsiField("CoNum")]
    public string? CoNum { get; set; }

    [CsiField("Item")]
    public string Item { get; set; }

    [CsiField("Description")]
    public string? ItemDesc { get; set; }

    [CsiField("Price")]
    public decimal Price { get; set; }

    [CsiField("QtyOrdered")]
    public decimal OrdQty { get; set; }


    [CsiField("QtyShipped")]
    public decimal ShippedQty { get; set; }

    [JsonIgnore]
    public decimal OpenQty =>
        Math.Max(0m, (OrdQty) - (ShippedQty));

    [JsonIgnore]
    public decimal OpenDollars => Math.Max(0m, (OrdQty) - (ShippedQty)) * Price;

    [CsiField("AdrName")]
    public string? ShipToName { get; set; }

    [CsiField("CoCustSeq")]
    public int ShipToNum { get; set; }

    [CsiField("SalesRegion")]
    public string? ShipToRegion { get; set; }


    // Updated Status Category based on DueDate relative to today + 30 days

}