using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper;

namespace RepPortal.Models;

public class CustomerShipment
{
    public string CustNum { get; set; }
    public string CustName { get; set; }
    public string PoNumber { get; set; }
    public string OrderNumber { get; set; }
    public decimal ExtLinePrice { get; set; }
    public int CoLine { get; set; }
    public string ItemNum { get; set; }
    public string ItemDesc { get; set; }
    public DateTime ShipDate { get; set; }
    public string CarrierCode { get; set; }
    public string ServiceType { get; set; }
    public int ShipQty { get; set; }
    public string? FreightTerms { get; set; }
    public string? BillToState { get; set; }
    public string? ShipToState { get; set; }
    public string? ShipToRegion { get; set; }



    public string ShipCode { get; set; }
    [Column("tracking_number")]
    public string TrackingNumber { get; set; }
    [Column("due_date")]
    public DateTime DueDate { get; set; }

    public string Whse { get; set; }

    [Column("shipment_id")]
    public int ShipmentID { get; set; }

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


