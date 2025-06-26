using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RepPortal.Models;

public class Pricebook
{
    public int PriceBookId { get; set; }
    public string PriceBookCode { get; set; } = string.Empty; // Default value added
    public string PriceBookName { get; set; } = string.Empty; // Default value added
    public DateTime EffectiveDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PriceBookItem
{
    public int PriceBookId { get; set; }
    public string Item { get; set; } = string.Empty; // Default value added
    public string Description { get; set; } = string.Empty;
    public decimal ListPrice { get; set; }
    public decimal PP1Price { get; set; }
    public decimal PP2Price { get; set; }
    public decimal BM1Price { get; set; }
    public decimal BM2Price { get; set; }
    public decimal FobPrice { get; set; }
    public decimal MSRPPrice { get; set; }
    public decimal MAPPrice { get; set; }

    public DateTime EffectiveDate { get; set; }
    public string? ItemStatus { get; set; }
}



