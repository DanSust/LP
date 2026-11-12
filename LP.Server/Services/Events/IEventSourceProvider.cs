using LP.Entity;

namespace LP.Server.Services.Events;

/// <summary>
/// Единый интерфейс источника событий. Чтобы добавить новый формат
/// (iCal, XML API, Telegram и т.п.), достаточно реализовать этот интерфейс
/// и зарегистрировать реализацию в DI.
/// </summary>
public interface IEventSourceProvider
{
    EventSourceType Type { get; }

    Task<IReadOnlyList<ImportedEvent>> FetchAsync(
        EventSource source,
        CancellationToken cancellationToken);
}
