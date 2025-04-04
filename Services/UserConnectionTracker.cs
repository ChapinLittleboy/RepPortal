namespace RepPortal.Services
{
    public class UserConnectionTracker
    {
        private readonly HashSet<string> _connections = new();

        public int ActiveConnections => _connections.Count;

        public void AddConnection(string connectionId)
        {
            lock (_connections)
            {
                _connections.Add(connectionId);
            }
        }

        public void RemoveConnection(string connectionId)
        {
            lock (_connections)
            {
                _connections.Remove(connectionId);
            }
        }
    }

}
