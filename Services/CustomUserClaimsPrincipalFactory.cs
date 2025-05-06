using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RepPortal.Data;
using System.Security.Claims;

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

        // ✅ Add role claims manually
        var roles = await UserManager.GetRolesAsync(user);
        var existingRoles = identity.FindAll(ClaimTypes.Role).Select(r => r.Value).ToHashSet();

        foreach (var role in roles)
        {
            Console.WriteLine($"Adding role claim: {role}"); // 🔍 For debugging

            if (!existingRoles.Contains(role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        // ✅ Add custom claims
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

        if (!string.IsNullOrEmpty(user.Region))
        {
            identity.AddClaim(new Claim("Region", user.Region));
        }
        // --- new: mirror LER ↔ LNE whenever exactly one is present ---
        var regions = identity.FindAll("Region")
            .Select(c => c.Value)
            .Distinct()
            .ToList();

        if (regions.Count == 1)
        {
            var single = regions[0];
            if (single == "LER")
                identity.AddClaim(new Claim("Region", "LNE"));
            else if (single == "LNE")
                identity.AddClaim(new Claim("Region", "LER"));
            else if (single == "LAW")
                identity.AddClaim(new Claim("Region", "LMW"));
            else if (single == "LMW")
                identity.AddClaim(new Claim("Region", "LAW"));
        }


        return identity;
    }
}