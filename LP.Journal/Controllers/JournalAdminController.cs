using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LP.Entity;
using LP.Journal.DTO.Journal;
using LP.Journal.Services.Journal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LP.Journal.Controllers;

/// <summary>
/// Админ-API журнала: черновики, источники, редактура, проверка атрибуции
/// и публикация. Доступ защищён заголовком X-Admin-Key
/// (Journal:AdminKey или Events:AdminKey).
/// </summary>
[ApiController]
[Route("Journal/admin")]
public sealed class JournalAdminController : ControllerBase
{
    private readonly ApplicationContext _context;
    private readonly IConfiguration _configuration;
    private readonly NewsSlugService _slugService;
    private readonly NewsDraftService _draftService;
    private readonly INewsDraftGenerator _draftGenerator;
    private readonly JournalCrawlService _crawlService;
    private readonly IEnumerable<IJournalSourceReader> _readers;

    public JournalAdminController(
        ApplicationContext context,
        IConfiguration configuration,
        NewsSlugService slugService,
        NewsDraftService draftService,
        INewsDraftGenerator draftGenerator,
        JournalCrawlService crawlService,
        IEnumerable<IJournalSourceReader> readers)
    {
        _context = context;
        _configuration = configuration;
        _slugService = slugService;
        _draftService = draftService;
        _draftGenerator = draftGenerator;
        _crawlService = crawlService;
        _readers = readers;
    }

    [HttpGet("config")]
    public ActionResult<JournalConfigDto> Config()
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        return Ok(new JournalConfigDto
        {
            AiConfigured = _draftGenerator.IsConfigured,
            AiModel = _draftGenerator.Model,
            MaxSimilarityPercent = MaxSimilarityPercent,
            CrawlEnabled = _configuration.GetValue("Journal:CrawlEnabled", true)
        });
    }

    [HttpGet("items")]
    public async Task<ActionResult<IReadOnlyList<AdminNewsDto>>> GetItems(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.NewsItems.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status) &&
            !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<NewsStatus>(status, true, out var parsedStatus))
            {
                return BadRequest(new { error = "Неизвестный статус. Допустимо: Draft, Published, Rejected, Archived, all." });
            }

            query = query.Where(item => item.Status == parsedStatus);
        }

        var items = await query
            .OrderByDescending(item => item.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(items.Select(ToAdminDto).ToList());
    }

    [HttpGet("items/{id:guid}")]
    public async Task<ActionResult<AdminNewsDto>> GetItem(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var item = await _context.NewsItems.AsNoTracking().FirstOrDefaultAsync(news => news.Id == id);
        return item is null ? NotFound() : Ok(ToAdminDto(item));
    }

    [HttpPost("items")]
    public async Task<ActionResult<AdminNewsDto>> CreateItem([FromBody] NewsUpsertDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var now = DateTime.UtcNow;
        var item = new NewsItem
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now,
            Status = NewsStatus.Draft
        };

        Apply(dto, item);
        item.Slug = await _slugService.GenerateUniqueAsync(item.Title, item.Id, HttpContext.RequestAborted);

        _context.NewsItems.Add(item);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, ToAdminDto(item));
    }

    [HttpPut("items/{id:guid}")]
    public async Task<ActionResult<AdminNewsDto>> UpdateItem(Guid id, [FromBody] NewsUpsertDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var item = await _context.NewsItems.FirstOrDefaultAsync(news => news.Id == id);
        if (item is null)
        {
            return NotFound();
        }

        Apply(dto, item);

        if (string.IsNullOrWhiteSpace(item.Slug))
        {
            item.Slug = await _slugService.GenerateUniqueAsync(
                item.Title,
                item.Id,
                HttpContext.RequestAborted);
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ToAdminDto(item));
    }

    [HttpPost("items/{id:guid}/publish")]
    public async Task<ActionResult<AdminNewsDto>> PublishItem(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var item = await _context.NewsItems.FirstOrDefaultAsync(news => news.Id == id);
        if (item is null)
        {
            return NotFound();
        }

        var problem = ValidateForPublish(item);
        if (problem is not null)
        {
            return BadRequest(new { error = problem });
        }

        var now = DateTime.UtcNow;
        item.Status = NewsStatus.Published;
        item.PublishedAt ??= now;
        item.ModeratedAt = now;
        item.ModeratedBy = "admin";
        item.RejectionReason = null;
        item.UpdatedAt = now;

        if (string.IsNullOrWhiteSpace(item.Slug))
        {
            item.Slug = await _slugService.GenerateUniqueAsync(
                item.Title,
                item.Id,
                HttpContext.RequestAborted);
        }

        await _context.SaveChangesAsync();
        return Ok(ToAdminDto(item));
    }

    [HttpPost("items/{id:guid}/unpublish")]
    public async Task<ActionResult<AdminNewsDto>> UnpublishItem(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var item = await _context.NewsItems.FirstOrDefaultAsync(news => news.Id == id);
        if (item is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        item.Status = NewsStatus.Draft;
        item.PublishedAt = null;
        item.ModeratedAt = now;
        item.ModeratedBy = "admin";
        item.UpdatedAt = now;

        await _context.SaveChangesAsync();
        return Ok(ToAdminDto(item));
    }

    [HttpPost("items/{id:guid}/reject")]
    public async Task<ActionResult<AdminNewsDto>> RejectItem(Guid id, [FromBody] RejectNewsDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var item = await _context.NewsItems.FirstOrDefaultAsync(news => news.Id == id);
        if (item is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        item.Status = NewsStatus.Rejected;
        item.PublishedAt = null;
        item.RejectionReason = dto.Reason.Trim();
        item.ModeratedAt = now;
        item.ModeratedBy = "admin";
        item.UpdatedAt = now;

        await _context.SaveChangesAsync();
        return Ok(ToAdminDto(item));
    }

    [HttpDelete("items/{id:guid}")]
    public async Task<ActionResult> DeleteItem(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var item = await _context.NewsItems.FirstOrDefaultAsync(news => news.Id == id);
        if (item is null)
        {
            return NotFound();
        }

        if (item.Status == NewsStatus.Published)
        {
            return BadRequest(new { error = "Опубликованный материал нельзя удалить. Сначала снимите его с публикации." });
        }

        _context.NewsItems.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Удаляет выбранные материалы одним запросом. В отличие от одиночного DELETE,
    /// позволяет удалять и опубликованные записи — для очистки журнала админом.
    /// </summary>
    [HttpDelete("items")]
    public async Task<ActionResult> DeleteItems([FromBody] IReadOnlyList<Guid>? ids)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var uniqueIds = ids?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (uniqueIds is null || uniqueIds.Count == 0)
        {
            return BadRequest(new { error = "Выберите материалы для удаления." });
        }

        var deleted = await _context.NewsItems
            .Where(item => uniqueIds.Contains(item.Id))
            .ExecuteDeleteAsync(HttpContext.RequestAborted);

        return Ok(new { deleted });
    }

    [HttpPost("draft-from-url")]
    public async Task<ActionResult<DraftFromUrlResultDto>> DraftFromUrl([FromBody] DraftFromUrlDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var result = await _draftService.CreateDraftAsync(dto, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("sources")]
    public async Task<ActionResult<IReadOnlyList<JournalSourceDto>>> GetSources()
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var sources = await _context.JournalSources
            .AsNoTracking()
            .OrderByDescending(source => source.UpdatedAt)
            .ToListAsync();

        return Ok(sources.Select(ToSourceDto).ToList());
    }

    [HttpGet("sources/{id:guid}")]
    public async Task<ActionResult<JournalSourceDto>> GetSource(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var source = await _context.JournalSources.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        return source is null ? NotFound() : Ok(ToSourceDto(source));
    }

    [HttpPost("sources")]
    public async Task<ActionResult<JournalSourceDto>> CreateSource([FromBody] JournalSourceUpsertDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var now = DateTime.UtcNow;
        var source = new JournalSource
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var error = ApplySource(dto, source);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        _context.JournalSources.Add(source);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSource), new { id = source.Id }, ToSourceDto(source));
    }

    [HttpPut("sources/{id:guid}")]
    public async Task<ActionResult<JournalSourceDto>> UpdateSource(
        Guid id,
        [FromBody] JournalSourceUpsertDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var source = await _context.JournalSources.FirstOrDefaultAsync(item => item.Id == id);
        if (source is null)
        {
            return NotFound();
        }

        var error = ApplySource(dto, source);
        if (error is not null)
        {
            return BadRequest(new { error });
        }

        await _context.SaveChangesAsync();
        return Ok(ToSourceDto(source));
    }

    [HttpDelete("sources/{id:guid}")]
    public async Task<ActionResult> DeleteSource(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var source = await _context.JournalSources.FirstOrDefaultAsync(item => item.Id == id);
        if (source is null)
        {
            return NotFound();
        }

        _context.JournalSources.Remove(source);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("sources/{id:guid}/test")]
    public async Task<ActionResult<JournalSourceTestResultDto>> TestSource(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var source = await _context.JournalSources.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (source is null)
        {
            return NotFound();
        }

        var reader = _readers.FirstOrDefault(item => item.Type == source.Type);
        if (reader is null)
        {
            return Ok(new JournalSourceTestResultDto
            {
                Success = false,
                Error = "Для этого типа источника не зарегистрирован читатель."
            });
        }

        try
        {
            var items = await reader.ReadAsync(source, HttpContext.RequestAborted);

            return Ok(new JournalSourceTestResultDto
            {
                Success = true,
                ItemCount = items.Count,
                Items = items.Take(5).Select(item => new JournalSourceTestItemDto
                {
                    Title = item.Title,
                    Url = item.SourceUrl,
                    PublishedAt = ToUtc(item.PublishedAt)
                }).ToList()
            });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Ok(new JournalSourceTestResultDto
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    [HttpPost("sources/{id:guid}/sync")]
    public async Task<ActionResult<JournalSyncResultDto>> SyncSource(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var source = await _context.JournalSources.FirstOrDefaultAsync(item => item.Id == id);
        if (source is null)
        {
            return NotFound();
        }

        var result = await _crawlService.SyncSourceAsync(
            source,
            force: true,
            maxDrafts: null,
            HttpContext.RequestAborted);

        return Ok(ToSyncDto(result));
    }

    [HttpPost("sources/sync")]
    public async Task<ActionResult<JournalSyncResultDto>> SyncAllSources()
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var result = await _crawlService.SyncAllAsync(force: true, HttpContext.RequestAborted);
        return Ok(ToSyncDto(result));
    }

    private int MaxSimilarityPercent =>
        Math.Clamp(_configuration.GetValue("Journal:MaxSimilarityPercent", 20), 1, 100);

    private string? ValidateForPublish(NewsItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Title))
        {
            return "У материала нет заголовка.";
        }

        if (string.IsNullOrWhiteSpace(item.Body))
        {
            return "У материала нет текста.";
        }

        if (!item.FactsChecked)
        {
            return "Подтвердите, что факты проверены.";
        }

        if (!item.AttributionChecked)
        {
            return "Подтвердите, что источник указан или материал полностью оригинальный.";
        }

        if (!string.IsNullOrWhiteSpace(item.SourceUrl) && string.IsNullOrWhiteSpace(item.SourceName))
        {
            return "Укажите название источника вместе со ссылкой.";
        }

        if (!string.IsNullOrWhiteSpace(item.HeroImageUrl) && !item.ImageRightsChecked)
        {
            return "Подтвердите права на изображение.";
        }

        if (item.AiGenerated && item.SimilarityPercent is null)
        {
            return "Для AI-черновика нужна проверка на пересечение с источником.";
        }

        if (item.SimilarityPercent is { } similarity && similarity > MaxSimilarityPercent)
        {
            return $"Пересечение с источником {similarity}% превышает допустимые {MaxSimilarityPercent}%. Перепишите текст.";
        }

        return null;
    }

    private static void Apply(NewsUpsertDto dto, NewsItem item)
    {
        item.Title = dto.Title.Trim();
        item.Summary = TrimOrNull(dto.Summary);
        item.Body = JournalTextCleaner.NormalizeBody(dto.Body);
        item.Topic = TrimOrNull(dto.Topic);
        item.Tags = TrimOrNull(dto.Tags);
        item.City = TrimOrNull(dto.City);
        item.HeroImageUrl = TrimOrNull(dto.HeroImageUrl);
        item.ImageLicense = TrimOrNull(dto.ImageLicense);
        item.SourceName = TrimOrNull(dto.SourceName);
        item.SourceUrl = TrimOrNull(dto.SourceUrl);
        item.OriginalTitle = TrimOrNull(dto.OriginalTitle);
        item.SourcePublishedAt = ToUtc(dto.SourcePublishedAt);
        item.AuthorName = TrimOrNull(dto.AuthorName);
        item.EditorNotes = TrimOrNull(dto.EditorNotes);
        item.FactsChecked = dto.FactsChecked;
        item.AttributionChecked = dto.AttributionChecked;
        item.ImageRightsChecked = dto.ImageRightsChecked;
        item.SimilarityPercent = dto.SimilarityPercent;
        item.AiModel = TrimOrNull(dto.AiModel);
        item.PromptVersion = TrimOrNull(dto.PromptVersion);
        item.AiGenerated = dto.AiGenerated;
        item.UpdatedAt = DateTime.UtcNow;
    }

    private static string? ApplySource(JournalSourceUpsertDto dto, JournalSource source)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return "Укажите название источника.";
        }

        if (!JournalUrlGuard.TryGetPublicHttpUri(dto.Url, out _))
        {
            return "Укажите публичный http(s) URL источника.";
        }

        if (!string.IsNullOrWhiteSpace(dto.IncludePattern))
        {
            try
            {
                _ = new Regex(
                    dto.IncludePattern.Trim(),
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(2));
            }
            catch (ArgumentException ex)
            {
                return "Некорректное регулярное выражение в IncludePattern: " + ex.Message;
            }
        }

        source.Name = dto.Name.Trim();
        source.Type = dto.Type;
        source.Url = dto.Url!.Trim();
        source.IsEnabled = dto.IsEnabled;
        source.Topic = TrimOrNull(dto.Topic);
        source.City = TrimOrNull(dto.City);
        source.DefaultTags = JournalTextCleaner.NormalizeTags(dto.DefaultTags);
        source.SyncIntervalMinutes = Math.Clamp(dto.SyncIntervalMinutes, 15, 10080);
        source.MaxItemsPerSync = Math.Clamp(dto.MaxItemsPerSync, 1, 50);
        source.IncludePattern = TrimOrNull(dto.IncludePattern);
        source.AiDrafts = dto.AiDrafts;
        source.UpdatedAt = DateTime.UtcNow;

        return null;
    }

    private static AdminNewsDto ToAdminDto(NewsItem item)
    {
        return new AdminNewsDto
        {
            Id = item.Id,
            Slug = item.Slug,
            Title = item.Title,
            Summary = item.Summary,
            Body = item.Body,
            Topic = item.Topic,
            Tags = item.Tags,
            City = item.City,
            HeroImageUrl = item.HeroImageUrl,
            ImageLicense = item.ImageLicense,
            SourceId = item.SourceId,
            ExternalId = item.ExternalId,
            SourceName = item.SourceName,
            SourceUrl = item.SourceUrl,
            OriginalTitle = item.OriginalTitle,
            SourcePublishedAt = ToUtc(item.SourcePublishedAt),
            AiModel = item.AiModel,
            PromptVersion = item.PromptVersion,
            AiGenerated = item.AiGenerated,
            SimilarityPercent = item.SimilarityPercent,
            AuthorName = item.AuthorName,
            EditorNotes = item.EditorNotes,
            Status = item.Status.ToString(),
            FactsChecked = item.FactsChecked,
            AttributionChecked = item.AttributionChecked,
            ImageRightsChecked = item.ImageRightsChecked,
            CreatedAt = ToUtc(item.CreatedAt)!.Value,
            UpdatedAt = ToUtc(item.UpdatedAt)!.Value,
            PublishedAt = ToUtc(item.PublishedAt),
            RejectionReason = item.RejectionReason
        };
    }

    private static JournalSourceDto ToSourceDto(JournalSource source)
    {
        return new JournalSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            Type = source.Type,
            Url = source.Url,
            IsEnabled = source.IsEnabled,
            Topic = source.Topic,
            City = source.City,
            DefaultTags = source.DefaultTags,
            SyncIntervalMinutes = source.SyncIntervalMinutes,
            MaxItemsPerSync = source.MaxItemsPerSync,
            IncludePattern = source.IncludePattern,
            AiDrafts = source.AiDrafts,
            LastSyncAt = ToUtc(source.LastSyncAt),
            LastSyncStatus = source.LastSyncStatus,
            LastSyncError = source.LastSyncError,
            CreatedAt = ToUtc(source.CreatedAt)!.Value,
            UpdatedAt = ToUtc(source.UpdatedAt)!.Value
        };
    }

    private static JournalSyncResultDto ToSyncDto(JournalSyncResult result)
    {
        return new JournalSyncResultDto
        {
            Success = result.Success,
            Created = result.Created,
            Skipped = result.Skipped,
            Status = result.Status,
            Error = result.Error
        };
    }

    private ActionResult? EnsureAdmin()
    {
        var configuredKey = _configuration["Journal:AdminKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            configuredKey = _configuration["Events:AdminKey"];
        }

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return StatusCode(503, new
            {
                error = "Админ-доступ к журналу не настроен. Задайте Journal:AdminKey (или Events:AdminKey)."
            });
        }

        var providedKey = Request.Headers["X-Admin-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return Unauthorized(new { error = "Нужен заголовок X-Admin-Key." });
        }

        var expected = Encoding.UTF8.GetBytes(configuredKey);
        var actual = Encoding.UTF8.GetBytes(providedKey);

        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return Unauthorized(new { error = "Неверный админ-ключ." });
        }

        return null;
    }

    private static DateTime? ToUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
