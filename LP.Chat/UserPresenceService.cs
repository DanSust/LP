using LP.Chat.Interfaces;
using System.Collections.Concurrent;

namespace LP.Chat;
public class UserPresenceService : IUserPresenceService
{
    // userId -> connectionCount (поддерживаем множественные подключения одного пользователя)
    private readonly ConcurrentDictionary<Guid, int> _onlineUsers = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionToUser = new();

    public void UserConnected(Guid userId, string connectionId)
    {
        _connectionToUser.TryAdd(connectionId, userId);
        _onlineUsers.AddOrUpdate(userId, 1, (_, count) => count + 1);
    }

    public void UserDisconnected(Guid userId, string connectionId)
    {
        _connectionToUser.TryRemove(connectionId, out _);

        if (_onlineUsers.TryGetValue(userId, out var count))
        {
            if (count <= 1)
                _onlineUsers.TryRemove(userId, out _);
            else
                _onlineUsers.TryUpdate(userId, count - 1, count);
        }
    }

    public bool IsUserOnline(Guid userId) => _onlineUsers.ContainsKey(userId);

    public IReadOnlyList<Guid> GetOnlineUsers() => _onlineUsers.Keys.ToList();

    public int GetOnlineUserCount() => _onlineUsers.Count;
}