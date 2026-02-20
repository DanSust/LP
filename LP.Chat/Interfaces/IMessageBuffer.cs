using LP.Entity;

namespace LP.Chat.Interfaces;

public interface IMessageBuffer
{
    Task AddMessageAsync(Message message);
    Task ForceFlushAsync();
}