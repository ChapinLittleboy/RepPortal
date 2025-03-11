using Microsoft.AspNetCore.Identity;

namespace RepPortal.Data;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string RepCode { get; set; }

}



