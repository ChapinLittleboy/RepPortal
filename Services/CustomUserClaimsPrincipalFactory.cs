namespace RepPortal.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RepPortal.Data;
using System.Security.Claims;
using System.Threading.Tasks;

public class CustomUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser>
{
    public CustomUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Add your custom claims here
        if (!string.IsNullOrEmpty(user.RepCode))
        {
            identity.AddClaim(new Claim("RepCode", user.RepCode));
        }

        if (!string.IsNullOrEmpty(user.FirstName))
        {
            identity.AddClaim(new Claim("FirstName", user.FirstName));
        }

        if (!string.IsNullOrEmpty(user.LastName))
        {
            identity.AddClaim(new Claim("LastName", user.LastName));
        }

        return identity;
    }
}
