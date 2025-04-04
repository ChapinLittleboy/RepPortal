using Microsoft.AspNetCore.Components.Server.Circuits;

namespace RepPortal.Services;

    

    public class TrackingCircuitHandler : CircuitHandler
    {
        private readonly UserConnectionTracker _tracker;

        public TrackingCircuitHandler(UserConnectionTracker tracker)
        {
            _tracker = tracker;
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
    }


