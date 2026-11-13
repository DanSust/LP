using LP.Entity;

namespace LP.Journal.Services.Journal;

/// <summary>
/// Нормализованный материал, найденный в источнике.
/// </summary>
public sealed record JournalSourceItem
{
    public string? ExternalId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Content { get; init; }
    public string? SourceUrl { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? Tags { get; init; }
}

/// <summary>
/// Читатель источника журнала. RSS/Atom и HTML-страницы реализуют один интерфейс,
/// поэтому новые форматы добавляются без изменений в краулере.
/// </summary>
public interface IJournalSourceReader
{
    JournalSourceType Type { get; }

    Task<IReadOnlyList<JournalSourceItem>> ReadAsync(
        JournalSource source,
        CancellationToken cancellationToken);
}
