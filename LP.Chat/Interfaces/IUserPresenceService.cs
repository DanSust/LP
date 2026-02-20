using System.Collections.Concurrent;

namespace LP.Chat.Interfaces;

public interface IUserPresenceService
{
    void UserConnected(Guid userId, string connectionId);
    void UserDisconnected(Guid userId, string connectionId);
    bool IsUserOnline(Guid userId);
    IReadOnlyList<Guid> GetOnlineUsers();
    int GetOnlineUserCount();
}