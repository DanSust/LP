using LP.Entity;

namespace LP.Chat.Interfaces
{
    // Можно положить прямо в папку Chat/ или Interfaces/
    public class NullMessageCache : IMessageCache
    {
        public Task<List<Message>> GetHistoryAsync(Guid chatId, int page = 0, int pageSize = 50)
        {
            return Task.FromResult(new List<Message>());
        }

        public Task AddToCacheAsync(Message message)
        {
            return Task.CompletedTask;
        }
    }
}
