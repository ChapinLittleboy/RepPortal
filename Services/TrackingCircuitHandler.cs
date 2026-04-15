using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using RepPortal.Data;

namespace RepPortal.Services;

public class TrackingCircuitHandler : CircuitHandler
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly UserConnectionTracker _tracker;
    private readonly UserManager<ApplicationUser> _userManager;

    public TrackingCircuitHandler(
        UserConnectionTracker tracker,
        AuthenticationStateProvider authenticationStateProvider,
        UserManager<ApplicationUser> userManager)
    {
        _tracker = tracker;
        _authenticationStateProvider = authenticationStateProvider;
        _userManager = userManager;
    }

    public override async Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var principal = authState.User;

        string? userId = null;
        string? email = null;
        string? repCode = null;

        if (principal.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(principal);
            userId = user?.Id;
            email = user?.Email ?? principal.Identity?.Name;
            repCode = user?.RepCode;
        }

        _tracker.AddConnection(circuit.Id, userId, email, repCode);
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _tracker.RemoveConnection(circuit.Id);
        return Task.CompletedTask;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        return async context =>
        {
            _tracker.MarkActivity(context.Circuit.Id);
            await next(context);
        };
    }
}
