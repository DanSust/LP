using LP.Journal.DTO.Journal;

namespace LP.Journal.Services.Journal;

/// <summary>
/// Собирает черновик из ссылки: загрузка источника, AI-генерация и проверка
/// на пересечение с оригиналом. Ничего не сохраняет — редактор сначала смотрит текст.
/// Без настроенного AI возвращает заготовку с метаданными источника и пустым телом.
/// </summary>
public sealed class NewsDraftService
{
    private readonly ArticleFetcher _fetcher;
    private readonly INewsDraftGenerator _generator;
    private readonly NewsSimilarityService _similarity;
    private readonly ILogger<NewsDraftService> _logger;

    public NewsDraftService(
        ArticleFetcher fetcher,
        INewsDraftGenerator generator,
        NewsSimilarityService similarity,
        ILogger<NewsDraftService> logger)
    {
        _fetcher = fetcher;
        _generator = generator;
        _similarity = similarity;
        _logger = logger;
    }

    public async Task<DraftFromUrlResultDto> CreateDraftAsync(
        DraftFromUrlDto dto,
        CancellationToken cancellationToken)
    {
        FetchedArticle article;

        try
        {
            article = await _fetcher.FetchAsync(dto.Url, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Journal source fetch failed for {Url}", dto.Url);
            return new DraftFromUrlResultDto
            {
                Success = false,
                Error = ex is TaskCanceledException
                    ? "Источник не ответил вовремя. Попробуйте позже."
                    : ex.Message
            };
        }

        var sourceName = article.SourceName;

        if (string.IsNullOrWhiteSpace(sourceName) &&
            Uri.TryCreate(article.SourceUrl, UriKind.Absolute, out var uri))
        {
            sourceName = uri.Host;
        }

        if (!_generator.IsConfigured)
        {
            return new DraftFromUrlResultDto
            {
                Success = true,
                Title = article.Title,
                Summary = JournalTextCleaner.Summarize(article.Text, 300),
                Body = string.Empty,
                SourceName = sourceName,
                SourceUrl = article.SourceUrl,
                OriginalTitle = article.Title,
                SourcePublishedAt = article.PublishedAt,
                AiGenerated = false
            };
        }

        var generation = await _generator.GenerateAsync(
            new NewsDraftRequest(
                article.SourceUrl,
                sourceName,
                article.Title,
                article.Text,
                dto.Topic,
                dto.City,
                dto.Instructions),
            cancellationToken);

        if (!generation.Success)
        {
            return new DraftFromUrlResultDto
            {
                Success = false,
                Error = generation.Error,
                SourceName = sourceName,
                SourceUrl = article.SourceUrl,
                OriginalTitle = article.Title,
                SourcePublishedAt = article.PublishedAt,
                AiGenerated = false
            };
        }

        var similarity = _similarity.CalculatePercent(generation.Body!, article.Text);

        return new DraftFromUrlResultDto
        {
            Success = true,
            Title = generation.Title,
            Summary = generation.Summary,
            Body = generation.Body,
            SourceName = sourceName,
            SourceUrl = article.SourceUrl,
            OriginalTitle = article.Title,
            SourcePublishedAt = article.PublishedAt,
            AiModel = generation.Model,
            PromptVersion = generation.PromptVersion,
            AiGenerated = true,
            SimilarityPercent = similarity
        };
    }
}
