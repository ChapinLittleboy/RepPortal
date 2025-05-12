namespace RepPortal.Models;

public class CustomerOrderSummary
{
    public string Cust { get; set; } // Customer Number
    public string Name { get; set; } // Customer Name (CorpName)
    public int ShippableUnits { get; set; } // Shippable_U
    public int FutureUnits { get; set; } // Future_U
    public int TotalUnits { get; set; } // Total_U
    public decimal ShippableDollars { get; set; } // Shippable_D
    public decimal FutureDollars { get; set; } // Future_D
    public decimal TotalDollars { get; set; } // Total_D

    // This will hold the details when fetched for the hierarchy
    // It's initially null or empty and populated on demand (or pre-loaded)
    public List<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

// In your Models folder (e.g., Models/OrderDetail.cs)
public class OrderDetail
{
    public string Cust { get; set; }
    public string CustName { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime OrdDate { get; set; }
    public DateTime? PromDate { get; set; }
    public string? CustPO { get; set; }
    public string? CoNum { get; set; }
    public string Item { get; set; }
    public string? ItemDesc { get; set; }

    public decimal Price { get; set; }
    public int OrdQty { get; set; }
    public int OpenQty { get; set; }
    public decimal OpenDollars { get; set; }
    public string? ShipToName { get; set; }
    public int ShipToNum { get; set; }
    public string? ShipToRegion { get; set; }


    // Updated Status Category based on DueDate relative to today + 30 days

}
public class CustomerOrderSummaryExport
{
    public string Cust { get; set; } // Customer Number
    public string Name { get; set; } // Customer Name (CorpName)
    public int ShippableUnits { get; set; } // Shippable_U
    public int FutureUnits { get; set; } // Future_U
    public int TotalUnits { get; set; } // Total_U
    public decimal ShippableDollars { get; set; } // Shippable_D
    public decimal FutureDollars { get; set; } // Future_D
    public decimal TotalDollars { get; set; } // Total_D
}