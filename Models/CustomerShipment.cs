using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper;

namespace RepPortal.Models;

public class CustomerShipment
{
    // ===== Customer / Order info =====

    [CsiField("CoCustNum")]
    public string CustNum { get; set; } = "";

    [CsiField("CadrName")]
    public string CustName { get; set; } = "";

    [CsiField("CoCustPo")]
    public string PoNumber { get; set; } = "";

    [CsiField("CoNum")]
    public string OrderNumber { get; set; } = "";

    // ===== Line / Item info =====

    [CsiField("CoLine")]
    public int CoLine { get; set; }

    [CsiField("CoiItem")]
    public string ItemNum { get; set; } = "";

    [CsiField("CoiDescription")]
    public string ItemDesc { get; set; } = "";

    // ===== Dates =====

    [CsiField("ShipDate")]
    public DateTime ShipDate { get; set; }

    // Due date (again there are CoiDueDate and CoihDueDate; this is safest)
    [CsiField("CoiDueDate")]
    [Column("due_date")]
    public DateTime DueDate { get; set; }

    // ===== Quantities / Pricing =====

    // Qty shipped
    // SLCoShips has QtyShipped and also ShipmentQtyShipped / DerQtyShippedConv.
    // QtyShipped is most common.
    [CsiField("QtyShipped")]
    public int ShipQty { get; set; }

    [CsiField("DerNetPrice")]
    public decimal Price { get; set; }

    // Option A: Map ExtLinePrice to DerTotPrice (already calculated total)
    // Option B: Calculate later as: Price * ShipQty (preferred because your SP did qty_shipped * ci.price)
    [CsiField("DerTotPrice")]
    public decimal ExtLinePrice { get; set; }

    // ===== Address / Region =====

    [CsiField("DerBillToState")]
    public string? BillToState { get; set; }

    // There’s no obvious DerShipToState in your list.
    // If SLCoShips doesn’t expose it, this one might stay null and come from another source.
    // Leaving it unmapped for now.
    public string? ShipToState { get; set; }

    [CsiField("DerShipToSalesRegion")]
    public string? ShipToRegion { get; set; }

    // ===== Shipping / Logistics =====
    // These exist in SLCoShips but you said you will ultimately override from AIT_SS_BOLs.

    [CsiField("ShpCarrierCode")]
    public string CarrierCode { get; set; } = "";

    // Service type is not cleanly exposed as a single field in SLCoShips list,
    // but you have DerCarrier and CarrierName, etc. Your AIT_SS_BOLs has Chap_CarrierServiceType.
    // Leave unmapped here (populated from BOLs).
    public string ServiceType { get; set; } = "";

    // FreightTerms (you’re using BillTransportationTo from AIT_SS_BOLs)
    public string? FreightTerms { get; set; }

    [CsiField("DoNum")] // DO/BOL in SLCoShips (often ship code)
    public string ShipCode { get; set; } = "";

    [CsiField("TrackingNumber")]
    [Column("tracking_number")]
    public string TrackingNumber { get; set; } = "";

    // Warehouse
    // SLCoShips exposes ShipperNum, PackNum, but not a plain Whse field in the snippet shown,
    // but your AIT_SS_BOLs includes Whse, so you can populate from that.
    public string Whse { get; set; } = "";

    // Shipment ID (SLCoShips: ShipmentId)
    [CsiField("ShipmentId")]
    [Column("shipment_id")]
    public int ShipmentID { get; set; }

    [CsiField("BolNumber")]
    public int? BolNumber { get; set; }

    // ===== Unused / not in SLCoShips list =====
    public string? ShipToSalesRegion { get; set; } // (you have ShipToRegion already)

    static CustomerShipment()
    {
        SqlMapper.SetTypeMap(typeof(CustomerShipment), new CustomPropertyTypeMap(
            typeof(CustomerShipment),
            (type, columnName) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(prop => prop.GetCustomAttribute<ColumnAttribute>()?.Name == columnName
                                     || prop.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase))));
    }

    public CustomerShipment()
    {
    }
}


