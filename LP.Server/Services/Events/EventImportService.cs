using System.Security.Cryptography;
using System.Text;
using LP.Entity;
using Microsoft.EntityFrameworkCore;

namespace LP.Server.Services.Events;

public sealed record EventImportResult(
    int Created,
    int Updated,
    int Skipped,
    string? Error)
{
    public bool Success => Error is null;
}

/// <summary>
/// Забирает события из включённых источников и складывает их в черновики.
/// Автопубликации нет: каждое событие проходит модерацию в админке.
/// </summary>
public sealed class EventImportService
{
    private readonly ApplicationContext _context;
    private readonly IEnumerable<IEventSourceProvider> _providers;
    private readonly EventSlugService _slugService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EventImportService> _logger;

    public EventImportService(
        ApplicationContext context,
        IEnumerable<IEventSourceProvider> providers,
        EventSlugService slugService,
        IConfiguration configuration,
        ILogger<EventImportService> logger)
    {
        _context = context;
        _providers = providers;
        _slugService = slugService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<int> SyncDueSourcesAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sources = await _context.EventSources
            .Where(source => source.IsEnabled && source.Type != EventSourceType.Manual)
            .ToListAsync(cancellationToken);

        var due = sources
            .Where(source =>
            {
                var interval = TimeSpan.FromMinutes(Math.Max(15, source.SyncIntervalMinutes));
                return source.LastSyncAt is null || source.LastSyncAt.Value + interval <= now;
            })
            .Take(10)
            .ToList();

        foreach (var source in due)
        {
            await SyncSourceAsync(source, cancellationToken);
        }

        return due.Count;
    }

    public async Task<EventImportResult> SyncSourceAsync(
        EventSource source,
        CancellationToken cancellationToken)
    {
        if (source.Type == EventSourceType.Manual)
        {
            return new EventImportResult(0, 0, 0, null);
        }

        var provider = _providers.FirstOrDefault(item => item.Type == source.Type);
        if (provider is null)
        {
            var error = $"Для типа источника «{source.Type}» не зарегистрирован провайдер.";
            await MarkFailedAsync(source, error, cancellationToken);
            return new EventImportResult(0, 0, 0, error);
        }

        try
        {
            var items = await provider.FetchAsync(source, cancellationToken);
            var maxItems = Math.Max(1, _configuration.GetValue("Events:MaxItemsPerSync", 100));
            var reservedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var created = 0;
            var updated = 0;
            var skipped = 0;

            foreach (var item in items.Take(maxItems))
            {
                var title = TextCleaner.Clean(item.Title, 200);
                if (string.IsNullOrWhiteSpace(title) || item.StartsAt == default)
                {
                    skipped++;
                    continue;
                }

                var externalId = BuildExternalId(source, item, title);
                var sourceUrl = TextCleaner.Clean(item.SourceUrl, 1000);

                var existing = await _context.Events.FirstOrDefaultAsync(
                    e => e.SourceId == source.Id && e.ExternalId == externalId,
                    cancellationToken);

                if (existing is null && !string.IsNullOrWhiteSpace(sourceUrl))
                {
                    existing = await _context.Events.FirstOrDefaultAsync(
                        e => e.SourceId == source.Id && e.SourceUrl == sourceUrl,
                        cancellationToken);
                }

                if (existing is not null)
                {
                    if (existing.Status == EventStatus.Draft)
                    {
                        ApplyImportedValues(existing, item, source, title, sourceUrl, externalId);
                        updated++;
                    }
                    else
                    {
                        skipped++;
                    }

                    continue;
                }

                var slug = await _slugService.GenerateUniqueAsync(
                    title,
                    null,
                    cancellationToken,
                    reservedSlugs);

                var newEvent = new Event
                {
                    Id = Guid.NewGuid(),
                    Slug = slug,
                    Status = EventStatus.Draft,
                    IsActive = true,
                    SourceId = source.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                ApplyImportedValues(newEvent, item, source, title, sourceUrl, externalId);
                _context.Events.Add(newEvent);
                created++;
            }

            await _context.SaveChangesAsync(cancellationToken);

            source.LastSyncAt = DateTime.UtcNow;
            source.LastSyncStatus = $"OK: новых {created}, обновлено {updated}, пропущено {skipped}";
            source.LastSyncError = null;
            source.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Event source {Source} synced: {Created} created, {Updated} updated, {Skipped} skipped",
                source.Name,
                created,
                updated,
                skipped);

            return new EventImportResult(created, updated, skipped, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync event source {Source}", source.Name);
            await MarkFailedAsync(source, ex.Message, cancellationToken);
            return new EventImportResult(0, 0, 0, ex.Message);
        }
    }

    private async Task MarkFailedAsync(
        EventSource source,
        string error,
        CancellationToken cancellationToken)
    {
        source.LastSyncAt = DateTime.UtcNow;
        source.LastSyncStatus = "ERROR";
        source.LastSyncError = error.Length <= 2000 ? error : error[..2000];
        source.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyImportedValues(
        Event target,
        ImportedEvent item,
        EventSource source,
        string title,
        string? sourceUrl,
        string externalId)
    {
        target.Title = title;
        target.Description = TextCleaner.Clean(item.Description, 4000) ?? string.Empty;
        target.Summary = TextCleaner.Clean(item.Summary, 600)
                         ?? TextCleaner.Summarize(target.Description, 300);
        target.StartsAt = item.StartsAt;
        target.EndsAt = item.EndsAt;
        target.City = TextCleaner.Clean(item.City, 120) ?? TextCleaner.Clean(source.City, 120);
        target.Venue = TextCleaner.Clean(item.Venue, 250);
        target.Address = TextCleaner.Clean(item.Address, 400);
        target.ImageUrl = TextCleaner.Clean(item.ImageUrl, 1000);
        target.SourceUrl = sourceUrl;
        target.ExternalId = externalId;
        target.Tags = EventSourceHelpers.MergeTags(item.Tags, source.DefaultTags);
        target.UpdatedAt = DateTime.UtcNow;
    }

    private static string BuildExternalId(EventSource source, ImportedEvent item, string title)
    {
        var provided = TextCleaner.Clean(item.ExternalId, 300);
        if (!string.IsNullOrWhiteSpace(provided))
        {
            return provided;
        }

        var sourceUrl = TextCleaner.Clean(item.SourceUrl, 1000);
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            return sourceUrl.Length <= 300 ? sourceUrl : sourceUrl[..300];
        }

        var raw = $"{title}|{item.StartsAt:O}|{source.Id}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
