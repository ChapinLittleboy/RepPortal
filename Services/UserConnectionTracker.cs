namespace RepPortal.Services;

public sealed class UserConnectionTracker
{
    private readonly Dictionary<string, ConnectedUserInfo> _connections = new(StringComparer.Ordinal);

    public int ActiveConnections
    {
        get
        {
            lock (_connections)
            {
                return _connections.Count;
            }
        }
    }

    public void AddConnection(string connectionId, string? userId, string? email, string? repCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        var connectedAtUtc = DateTimeOffset.UtcNow;

        lock (_connections)
        {
            _connections[connectionId] = new ConnectedUserInfo
            {
                ConnectionId = connectionId,
                UserId = userId,
                Email = email,
                RepCode = repCode,
                ConnectedAtUtc = connectedAtUtc
            };
        }
    }

    public void MarkActivity(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_connections)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.LastActivityUtc = DateTimeOffset.UtcNow;
            }
        }
    }

    public void RemoveConnection(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_connections)
        {
            _connections.Remove(connectionId);
        }
    }

    public IReadOnlyList<ConnectedUserInfo> GetActiveConnections()
    {
        lock (_connections)
        {
            return _connections.Values
                .OrderByDescending(x => x.ConnectedAtUtc)
                .Select(x => x.Clone())
                .ToList();
        }
    }
}

public sealed class ConnectedUserInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? RepCode { get; set; }
    public DateTimeOffset ConnectedAtUtc { get; set; }
    public DateTimeOffset? LastActivityUtc { get; set; }

    public ConnectedUserInfo Clone()
    {
        return new ConnectedUserInfo
        {
            ConnectionId = ConnectionId,
            UserId = UserId,
            Email = Email,
            RepCode = RepCode,
            ConnectedAtUtc = ConnectedAtUtc,
            LastActivityUtc = LastActivityUtc
        };
    }
}
