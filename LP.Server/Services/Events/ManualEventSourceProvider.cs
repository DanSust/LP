using LP.Entity;

namespace LP.Server.Services.Events;

/// <summary>
/// Ручной источник: события добавляются модератором через админку.
/// </summary>
public sealed class ManualEventSourceProvider : IEventSourceProvider
{
    public EventSourceType Type => EventSourceType.Manual;

    public Task<IReadOnlyList<ImportedEvent>> FetchAsync(
        EventSource source,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ImportedEvent>>([]);
    }
}
