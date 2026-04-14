using System.Security.Claims;

namespace RepPortal.Services;

public sealed class UserConnectionTracker
{
    private readonly object _syncLock = new();
    private readonly Dictionary<string, ConnectionEntry> _connections = new(StringComparer.Ordinal);

    public event Action? ConnectionsChanged;

    public int ActiveConnections
    {
        get
        {
            lock (_syncLock)
            {
                return _connections.Count;
            }
        }
    }

    public int AuthenticatedUserCount => GetConnectedUsers().Count(x => !x.IsAnonymous);

    public IReadOnlyList<ConnectedUserSummary> GetConnectedUsers()
    {
        lock (_syncLock)
        {
            return _connections.Values
                .GroupBy(entry => entry.UserKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var latest = group
                        .OrderByDescending(x => x.LastSeenUtc)
                        .First();

                    return new ConnectedUserSummary(
                        latest.UserKey,
                        latest.DisplayName,
                        latest.Email,
                        latest.UserName,
                        latest.IsAnonymous,
                        group.Count(),
                        group.Max(x => x.LastSeenUtc));
                })
                .OrderBy(summary => summary.IsAnonymous)
                .ThenBy(summary => summary.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public void AddConnection(string connectionId)
    {
        UpdateConnectionUser(connectionId, user: null);
    }

    public void UpdateConnectionUser(string connectionId, ClaimsPrincipal? user)
    {
        lock (_syncLock)
        {
            _connections[connectionId] = BuildEntry(connectionId, user);
        }

        ConnectionsChanged?.Invoke();
    }

    public void RemoveConnection(string connectionId)
    {
        var removed = false;

        lock (_syncLock)
        {
            removed = _connections.Remove(connectionId);
        }

        if (removed)
        {
            ConnectionsChanged?.Invoke();
        }
    }

    private static ConnectionEntry BuildEntry(string connectionId, ClaimsPrincipal? user)
    {
        var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;
        var firstName = user?.FindFirst("FirstName")?.Value;
        var lastName = user?.FindFirst("LastName")?.Value;
        var fullName = string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        var email = user?.FindFirst(ClaimTypes.Email)?.Value
            ?? user?.FindFirst("emails")?.Value
            ?? user?.Identity?.Name;
        var userName = user?.Identity?.Name
            ?? user?.FindFirst(ClaimTypes.Name)?.Value
            ?? email;
        var displayName = !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : !string.IsNullOrWhiteSpace(email)
                ? email
                : "Anonymous";
        var userKey = isAuthenticated
            ? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? email
                ?? userName
                ?? connectionId
            : $"anonymous:{connectionId}";

        return new ConnectionEntry(
            connectionId,
            userKey,
            displayName,
            email,
            userName,
            !isAuthenticated,
            DateTimeOffset.UtcNow);
    }

    public sealed record ConnectedUserSummary(
        string UserKey,
        string DisplayName,
        string? Email,
        string? UserName,
        bool IsAnonymous,
        int ConnectionCount,
        DateTimeOffset LastSeenUtc);

    private sealed record ConnectionEntry(
        string ConnectionId,
        string UserKey,
        string DisplayName,
        string? Email,
        string? UserName,
        bool IsAnonymous,
        DateTimeOffset LastSeenUtc);
}
