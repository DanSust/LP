using LP.Entity;

namespace LP.Chat.Interfaces;

public interface IMessageCache
{
    Task<List<Message>> GetHistoryAsync(Guid chatId, int page = 0, int pageSize = 50);
    Task AddToCacheAsync(Message message);
}