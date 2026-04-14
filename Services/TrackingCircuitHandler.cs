using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace RepPortal.Services;

public sealed class TrackingCircuitHandler : CircuitHandler, IDisposable
{
    private readonly UserConnectionTracker _tracker;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private string? _circuitId;

    public TrackingCircuitHandler(
        UserConnectionTracker tracker,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _tracker = tracker;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _circuitId = circuit.Id;
        _authenticationStateProvider.AuthenticationStateChanged += HandleAuthenticationStateChanged;

        var authenticationState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        _tracker.UpdateConnectionUser(circuit.Id, authenticationState.User);
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _tracker.AddConnection(circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _tracker.RemoveConnection(circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _tracker.RemoveConnection(circuit.Id);
        _authenticationStateProvider.AuthenticationStateChanged -= HandleAuthenticationStateChanged;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _authenticationStateProvider.AuthenticationStateChanged -= HandleAuthenticationStateChanged;
    }

    private async void HandleAuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
    {
        if (string.IsNullOrWhiteSpace(_circuitId))
        {
            return;
        }

        var authenticationState = await authenticationStateTask;
        _tracker.UpdateConnectionUser(_circuitId, authenticationState.User);
    }
}
