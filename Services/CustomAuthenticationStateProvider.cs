using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using RepPortal.Data;

namespace RepPortal.Services;

public class CustomAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NavigationManager _nav;

    public CustomAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        NavigationManager nav)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _nav = nav;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(10);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(authenticationState.User);

        if (user == null || !user.IsActive)
        {
            // Redirect to access denied page if the user is inactive
            _nav.NavigateTo("/access-denied", forceLoad: true);
            return false; // this will also sign the user out
        }

        return true;
    }
}




