namespace RepPortal.Models;

public class BolHeader
{
    [CsiField("ShipmentId")]
    public int ShipmentId { get; set; }

    [CsiField("Chap_CarrierServiceType")]
    public string? ServiceType { get; set; }

    [CsiField("CarrierCode")]
    public string? CarrierCode { get; set; }

    [CsiField("ShipCode")]
    public string? ShipCode { get; set; }

    [CsiField("TrackingNumber")]
    public string? TrackingNumber { get; set; }

    [CsiField("Whse")]
    public string? Whse { get; set; }

    [CsiField("BillTransportationTo")]
    public string? BillTransportationTo { get; set; }
}
