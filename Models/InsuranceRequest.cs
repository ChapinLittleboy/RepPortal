using RepPortal.Data;

namespace RepPortal.Models;

public class InsuranceRequest
{
    public string RepCode { get; set; }
    public string? ExistingCustomerId { get; set; }
    public NewCustomerInfo NewCustomer { get; set; }
    public string Notes { get; set; }
    public List<IFormFile> Attachments { get; set; } = new();
}

public class NewCustomerInfo
{
    public string Name { get; set; } = default!;
    public string? Address { get; set; } = default!;
    public string? ContactName { get; set; } = default!;
    public string? Email { get; set; } = default!;
    public string? Phone { get; set; } = default!;
}