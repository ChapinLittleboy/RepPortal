namespace RepPortal.Models;

public class ConsolidatedCustomer
{
    public string CustNum { get; set; } // First part of the composite key
    public int CustSeq { get; set; }    // Second part of the composite key

    public string? Salesman { get; set; }
    public string? SalesManager { get; set; }

    public string? CustomerName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }

    // Navigation property for UserCustomerAccess relationships
    //public ICollection<UserCustomerAccess> UserCustomerAccesses { get; set; }
}


