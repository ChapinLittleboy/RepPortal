using Microsoft.AspNetCore.Identity;

namespace RepPortal.Data;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? RepCode { get; set; }
    public string? WindowsLogin { get; set; } // eg.Chapin\willit2
    public bool IsActive { get; set; } = false; // Default to true


}



