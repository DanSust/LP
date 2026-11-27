using System.Security.Cryptography;
using System.Text;
using LP.Entity;
using Microsoft.EntityFrameworkCore;

namespace LP.Journal.Services.Journal;

public sealed record JournalSyncResult(
    bool Success,
    int Created,
    int Skipped,
    string? Status,
    string? Error);

/// <summary>
/// Обходит включённые источники и создаёт черновики. Автопубликации нет:
/// каждый материал проходит ручную редактуру и проверку в админке.
/// </summary>
public sealed class JournalCrawlService
{
    private readonly ApplicationContext _context;
    private readonly IEnumerable<IJournalSourceReader> _readers;
    private readonly ArticleFetcher _articleFetcher;
    private readonly NewsSlugService _slugService;
    private readonly NewsSimilarityService _similarity;
    private readonly INewsDraftGenerator _generator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JournalCrawlService> _logger;

    public JournalCrawlService(
        ApplicationContext context,
        IEnumerable<IJournalSourceReader> readers,
        ArticleFetcher articleFetcher,
        NewsSlugService slugService,
        NewsSimilarityService similarity,
        INewsDraftGenerator generator,
        IConfiguration configuration,
        ILogger<JournalCrawlService> logger)
    {
        _context = context;
        _readers = readers;
        _articleFetcher = articleFetcher;
        _slugService = slugService;
        _similarity = similarity;
        _generator = generator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<int> SyncDueSourcesAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sources = await _context.JournalSources
            .Where(source => source.IsEnabled)
            .OrderBy(source => source.LastSyncAt)
            .ToListAsync(cancellationToken);

        var due = sources
            .Where(source => IsDue(source, now))
            .Take(10)
            .ToList();

        var remaining = Math.Max(1, _configuration.GetValue("Journal:MaxDraftsPerRun", 30));

        foreach (var source in due)
        {
            if (remaining <= 0)
            {
                break;
            }

            var result = await SyncSourceAsync(source, force: false, remaining, cancellationToken);
            remaining -= result.Created;
        }

        return due.Count;
    }

    public async Task<JournalSyncResult> SyncAllAsync(bool force, CancellationToken cancellationToken)
    {
        var sources = await _context.JournalSources
            .Where(source => source.IsEnabled)
            .OrderBy(source => source.LastSyncAt)
            .ToListAsync(cancellationToken);

        var remaining = Math.Max(1, _configuration.GetValue("Journal:MaxDraftsPerRun", 30));
        var created = 0;
        var skipped = 0;
        string? firstError = null;

        foreach (var source in sources)
        {
            if (remaining <= 0)
            {
                break;
            }

            var result = await SyncSourceAsync(source, force, remaining, cancellationToken);
            created += result.Created;
            skipped += result.Skipped;
            remaining -= result.Created;

            if (!result.Success && firstError is null)
            {
                firstError = result.Error;
            }
        }

        return new JournalSyncResult(
            firstError is null,
            created,
            skipped,
            $"Источников: {sources.Count}. Новых черновиков: {created}, пропущено: {skipped}.",
            firstError);
    }

    public async Task<JournalSyncResult> SyncSourceAsync(
        JournalSource source,
        bool force,
        int? maxDrafts,
        CancellationToken cancellationToken)
    {
        if (!force && !IsDue(source, DateTime.UtcNow))
        {
            return new JournalSyncResult(true, 0, 0, "Источник ещё не требует обновления.", null);
        }

        var reader = _readers.FirstOrDefault(item => item.Type == source.Type);
        if (reader is null)
        {
            const string error = "Для этого типа источника не зарегистрирован читатель.";
            await MarkFailedAsync(source, error, cancellationToken);
            return new JournalSyncResult(false, 0, 0, null, error);
        }

        var created = 0;
        var skipped = 0;

        try
        {
            var items = await reader.ReadAsync(source, cancellationToken);

            var sourceLimit = Math.Clamp(source.MaxItemsPerSync, 1, 50);
            var globalLimit = Math.Max(1, _configuration.GetValue("Journal:MaxItemsPerSource", 10));
            var limit = Math.Min(sourceLimit, globalLimit);

            if (maxDrafts is { } available)
            {
                limit = Math.Min(limit, Math.Max(0, available));
            }

            var reservedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var delayMs = Math.Clamp(
                _configuration.GetValue("Journal:RequestDelayMilliseconds", 1000),
                0,
                10000);

            foreach (var item in items.Take(limit))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var title = JournalTextCleaner.Clean(item.Title, 220);
                if (string.IsNullOrWhiteSpace(title))
                {
                    skipped++;
                    continue;
                }

                var sourceUrl = JournalTextCleaner.Clean(item.SourceUrl, 1000);
                var externalId = BuildExternalId(source, item, title, sourceUrl);

                var exists = await _context.NewsItems.AnyAsync(
                    news => news.SourceId == source.Id && news.ExternalId == externalId,
                    cancellationToken);

                if (!exists && !string.IsNullOrWhiteSpace(sourceUrl))
                {
                    exists = await _context.NewsItems.AnyAsync(
                        news => news.SourceId == source.Id && news.SourceUrl == sourceUrl,
                        cancellationToken);
                }

                if (exists)
                {
                    skipped++;
                    continue;
                }

                if (delayMs > 0 && created + skipped > 0)
                {
                    await Task.Delay(delayMs, cancellationToken);
                }

                var article = await TryFetchArticleAsync(sourceUrl, cancellationToken);
                var sourceText = article?.Text ?? item.Content ?? string.Empty;
                var summary = item.Summary;

                var keywords = JournalTextCleaner.ParseKeywords(source.DefaultTags);
                if (!JournalTextCleaner.MatchesAnyKeyword($"{title} {summary} {sourceText}", keywords))
                {
                    skipped++;
                    continue;
                }

                var body = string.Empty;
                var aiModel = (string?)null;
                var promptVersion = (string?)null;
                var aiGenerated = false;
                int? similarity = null;

                if (_generator.IsConfigured && source.AiDrafts && sourceText.Length >= 300)
                {
                    var generation = await _generator.GenerateAsync(
                        new NewsDraftRequest(
                            sourceUrl ?? string.Empty,
                            source.Name,
                            title,
                            sourceText,
                            source.Topic,
                            source.City,
                            "Черновик создаётся автоматически из источника. Проверь факты и атрибуцию."),
                        cancellationToken);

                    if (generation.Success && !string.IsNullOrWhiteSpace(generation.Body))
                    {
                        title = JournalTextCleaner.Clean(generation.Title, 220) ?? title;
                        summary = generation.Summary ?? summary;
                        body = JournalTextCleaner.NormalizeBody(generation.Body);
                        aiModel = generation.Model;
                        promptVersion = generation.PromptVersion;
                        aiGenerated = true;
                        similarity = _similarity.CalculatePercent(body, sourceText);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Journal AI draft failed for source {Source}: {Error}",
                            source.Name,
                            generation.Error);
                    }
                }

                var slug = await _slugService.GenerateUniqueAsync(
                    title,
                    null,
                    cancellationToken,
                    reservedSlugs);

                var sourceName = source.Name;
                if (string.IsNullOrWhiteSpace(sourceName))
                {
                    sourceName = article?.SourceName;
                }

                var news = new NewsItem
                {
                    Id = Guid.NewGuid(),
                    Slug = slug,
                    Title = title,
                    Summary = JournalTextCleaner.Clean(summary, 600),
                    Body = body,
                    Topic = JournalTextCleaner.Clean(source.Topic, 120),
                    Tags = JournalTextCleaner.NormalizeTags(item.Tags),
                    City = JournalTextCleaner.Clean(source.City, 120),
                    SourceId = source.Id,
                    ExternalId = externalId,
                    SourceName = JournalTextCleaner.Clean(sourceName, 200),
                    SourceUrl = sourceUrl,
                    OriginalTitle = JournalTextCleaner.Clean(item.Title, 300),
                    SourcePublishedAt = item.PublishedAt ?? article?.PublishedAt,
                    AiGenerated = aiGenerated,
                    AiModel = JournalTextCleaner.Clean(aiModel, 120),
                    PromptVersion = JournalTextCleaner.Clean(promptVersion, 60),
                    SimilarityPercent = similarity,
                    Status = NewsStatus.Draft,
                    EditorNotes = aiGenerated
                        ? "Черновик создан автоматически из источника. Проверьте факты, атрибуцию и права на изображение."
                        : "Заготовка создана автоматически из источника. Текст нужно написать вручную.",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.NewsItems.Add(news);
                await _context.SaveChangesAsync(cancellationToken);
                created++;
            }

            source.LastSyncAt = DateTime.UtcNow;
            source.LastSyncStatus = $"OK: новых {created}, пропущено {skipped}";
            source.LastSyncError = null;
            source.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Journal source {Source} synced: {Created} created, {Skipped} skipped",
                source.Name,
                created,
                skipped);

            return new JournalSyncResult(
                true,
                created,
                skipped,
                source.LastSyncStatus,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync journal source {Source}", source.Name);
            await MarkFailedAsync(source, ex.Message, cancellationToken);
            return new JournalSyncResult(false, created, skipped, "ERROR", ex.Message);
        }
    }

    private async Task<FetchedArticle?> TryFetchArticleAsync(
        string? sourceUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return null;
        }

        try
        {
            return await _articleFetcher.FetchAsync(sourceUrl, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Journal article fetch failed, using feed content: {Url}", sourceUrl);
            return null;
        }
    }

    private async Task MarkFailedAsync(
        JournalSource source,
        string error,
        CancellationToken cancellationToken)
    {
        source.LastSyncAt = DateTime.UtcNow;
        source.LastSyncStatus = "ERROR";
        source.LastSyncError = error.Length <= 2000 ? error : error[..2000];
        source.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsDue(JournalSource source, DateTime now)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(15, source.SyncIntervalMinutes));
        return source.LastSyncAt is null || source.LastSyncAt.Value + interval <= now;
    }

    private static string BuildExternalId(
        JournalSource source,
        JournalSourceItem item,
        string title,
        string? sourceUrl)
    {
        var provided = JournalTextCleaner.Clean(item.ExternalId, 300);
        if (!string.IsNullOrWhiteSpace(provided))
        {
            return provided;
        }

        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            return sourceUrl.Length <= 300 ? sourceUrl : sourceUrl[..300];
        }

        var raw = $"{source.Id}|{title}|{item.PublishedAt:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
