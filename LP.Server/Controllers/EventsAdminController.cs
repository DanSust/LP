using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LP.Entity;
using LP.Server.DTO.Events;
using LP.Server.Services.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LP.Server.Controllers;

/// <summary>
/// Админ-API событий: источники, тестовый импорт и модерация черновиков.
/// Доступ защищён заголовком X-Admin-Key и настройкой Events:AdminKey.
/// </summary>
[ApiController]
[Route("Events/admin")]
public sealed class EventsAdminController : ControllerBase
{
    private readonly ApplicationContext _context;
    private readonly IConfiguration _configuration;
    private readonly EventImportService _importService;
    private readonly EventSlugService _slugService;
    private readonly IEnumerable<IEventSourceProvider> _providers;
    private readonly ILogger<EventsAdminController> _logger;

    public EventsAdminController(
        ApplicationContext context,
        IConfiguration configuration,
        EventImportService importService,
        EventSlugService slugService,
        IEnumerable<IEventSourceProvider> providers,
        ILogger<EventsAdminController> logger)
    {
        _context = context;
        _configuration = configuration;
        _importService = importService;
        _slugService = slugService;
        _providers = providers;
        _logger = logger;
    }

    [HttpGet("sources")]
    public async Task<ActionResult<IReadOnlyList<EventSourceDto>>> GetSources()
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var sources = await _context.EventSources
            .AsNoTracking()
            .OrderBy(source => source.Name)
            .ToListAsync();

        return Ok(sources.Select(ToSourceDto).ToList());
    }

    [HttpPost("sources")]
    public async Task<ActionResult<EventSourceDto>> CreateSource([FromBody] EventSourceUpsertDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        if (!TryValidateSource(dto, out var type, out var validationError))
        {
            return BadRequest(new { error = validationError });
        }

        var source = new EventSource
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        ApplySourceValues(source, dto, type);
        _context.EventSources.Add(source);
        await _context.SaveChangesAsync();

        return Ok(ToSourceDto(source));
    }

    [HttpPut("sources/{id:guid}")]
    public async Task<ActionResult<EventSourceDto>> UpdateSource(Guid id, [FromBody] EventSourceUpsertDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var source = await _context.EventSources.FirstOrDefaultAsync(item => item.Id == id);
        if (source is null)
        {
            return NotFound();
        }

        if (!TryValidateSource(dto, out var type, out var validationError))
        {
            return BadRequest(new { error = validationError });
        }

        ApplySourceValues(source, dto, type);
        source.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ToSourceDto(source));
    }

    [HttpDelete("sources/{id:guid}")]
    public async Task<IActionResult> DeleteSource(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var source = await _context.EventSources.FirstOrDefaultAsync(item => item.Id == id);
        if (source is null)
        {
            return NotFound();
        }

        _context.EventSources.Remove(source);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("sources/{id:guid}/sync")]
    public async Task<ActionResult<EventImportResultDto>> SyncSource(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var source = await _context.EventSources.FirstOrDefaultAsync(item => item.Id == id);
        if (source is null)
        {
            return NotFound();
        }

        var result = await _importService.SyncSourceAsync(source, HttpContext.RequestAborted);
        return Ok(new EventImportResultDto
        {
            Success = result.Success,
            Created = result.Created,
            Updated = result.Updated,
            Skipped = result.Skipped,
            Error = result.Error
        });
    }

    [HttpPost("sources/{id:guid}/test")]
    public async Task<ActionResult<EventSourcePreviewDto>> TestSource(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var source = await _context.EventSources.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (source is null)
        {
            return NotFound();
        }

        var provider = _providers.FirstOrDefault(item => item.Type == source.Type);
        if (provider is null)
        {
            return Ok(new EventSourcePreviewDto
            {
                Success = false,
                Error = $"Для типа источника «{source.Type}» не зарегистрирован провайдер."
            });
        }

        try
        {
            var items = await provider.FetchAsync(source, HttpContext.RequestAborted);
            return Ok(new EventSourcePreviewDto
            {
                Success = true,
                Total = items.Count,
                Items = items.Take(10).Select(item => new EventSourcePreviewItemDto
                {
                    Title = item.Title,
                    Summary = item.Summary,
                    StartsAt = ToUtc(item.StartsAt),
                    EndsAt = ToUtc(item.EndsAt),
                    City = item.City,
                    Venue = item.Venue,
                    ImageUrl = item.ImageUrl,
                    SourceUrl = item.SourceUrl
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Event source test failed for {Source}", source.Name);
            return Ok(new EventSourcePreviewDto { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<AdminEventDto>>> GetEvents(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.Events
            .AsNoTracking()
            .Include(item => item.Source)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<EventStatus>(status, true, out var parsedStatus))
            {
                return BadRequest(new { error = "Неизвестный статус события." });
            }

            query = query.Where(item => item.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(item => item.Title.Contains(normalizedSearch));
        }

        var events = await query
            .OrderByDescending(item => item.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(events.Select(item => ToAdminDto(item, item.Source?.Name)).ToList());
    }

    [HttpPost("events")]
    public async Task<ActionResult<AdminEventDto>> CreateEvent([FromBody] AdminEventUpsertDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (dto.StartsAt == default)
        {
            return BadRequest(new { error = "Укажите дату и время события." });
        }

        var now = DateTime.UtcNow;
        var slug = await _slugService.GenerateUniqueAsync(
            dto.Title,
            null,
            HttpContext.RequestAborted);

        var item = new Event
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            CreatedAt = now,
            UpdatedAt = now,
            Status = EventStatus.Draft,
            IsActive = true
        };

        ApplyEventValues(item, dto);

        if (dto.PublishImmediately)
        {
            Publish(item, now);
        }

        _context.Events.Add(item);
        await _context.SaveChangesAsync();

        return Ok(ToAdminDto(item));
    }

    [HttpPut("events/{id:guid}")]
    public async Task<ActionResult<AdminEventDto>> UpdateEvent(Guid id, [FromBody] AdminEventUpsertDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var item = await _context.Events
            .Include(ev => ev.Source)
            .FirstOrDefaultAsync(ev => ev.Id == id);

        if (item is null)
        {
            return NotFound();
        }

        if (dto.StartsAt == default)
        {
            return BadRequest(new { error = "Укажите дату и время события." });
        }

        ApplyEventValues(item, dto);

        if (dto.PublishImmediately)
        {
            Publish(item, DateTime.UtcNow);
        }

        await _context.SaveChangesAsync();
        return Ok(ToAdminDto(item, item.Source?.Name));
    }

    [HttpPost("events/{id:guid}/publish")]
    public async Task<ActionResult<AdminEventDto>> PublishEvent(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var item = await _context.Events
            .Include(ev => ev.Source)
            .FirstOrDefaultAsync(ev => ev.Id == id);

        if (item is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(item.Slug))
        {
            item.Slug = await _slugService.GenerateUniqueAsync(
                item.Title,
                item.Id,
                HttpContext.RequestAborted);
        }

        Publish(item, DateTime.UtcNow);
        await _context.SaveChangesAsync();

        return Ok(ToAdminDto(item, item.Source?.Name));
    }

    [HttpPost("events/{id:guid}/unpublish")]
    public async Task<ActionResult<AdminEventDto>> UnpublishEvent(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var item = await _context.Events
            .Include(ev => ev.Source)
            .FirstOrDefaultAsync(ev => ev.Id == id);

        if (item is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        item.Status = EventStatus.Draft;
        item.IsActive = false;
        item.PublishedAt = null;
        item.ModeratedAt = now;
        item.ModeratedBy = "admin";
        item.UpdatedAt = now;
        await _context.SaveChangesAsync();

        return Ok(ToAdminDto(item, item.Source?.Name));
    }

    [HttpPost("events/{id:guid}/reject")]
    public async Task<ActionResult<AdminEventDto>> RejectEvent(Guid id, [FromBody] RejectEventDto dto)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var item = await _context.Events
            .Include(ev => ev.Source)
            .FirstOrDefaultAsync(ev => ev.Id == id);

        if (item is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        item.Status = EventStatus.Rejected;
        item.IsActive = false;
        item.RejectionReason = dto.Reason.Trim();
        item.ModeratedAt = now;
        item.ModeratedBy = "admin";
        item.UpdatedAt = now;
        await _context.SaveChangesAsync();

        return Ok(ToAdminDto(item, item.Source?.Name));
    }

    [HttpDelete("events/{id:guid}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        if (EnsureAdmin() is { } denied)
        {
            return denied;
        }

        var item = await _context.Events.FirstOrDefaultAsync(ev => ev.Id == id);
        if (item is null)
        {
            return NotFound();
        }

        if (item.Status == EventStatus.Published)
        {
            return BadRequest(new { error = "Опубликованное событие нельзя удалить. Сначала снимите его с публикации." });
        }

        _context.Events.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private ActionResult? EnsureAdmin()
    {
        var configuredKey = _configuration["Events:AdminKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return StatusCode(503, new
            {
                error = "Админ-доступ к событиям не настроен. Задайте Events:AdminKey в конфигурации."
            });
        }

        var providedKey = Request.Headers["X-Admin-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return Unauthorized(new { error = "Нужен заголовок X-Admin-Key." });
        }

        var expected = Encoding.UTF8.GetBytes(configuredKey);
        var actual = Encoding.UTF8.GetBytes(providedKey);

        if (expected.Length != actual.Length ||
            !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return Unauthorized(new { error = "Неверный админ-ключ." });
        }

        return null;
    }

    private static bool TryValidateSource(
        EventSourceUpsertDto dto,
        out EventSourceType type,
        out string? error)
    {
        type = EventSourceType.Rss;
        error = null;

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            error = "Укажите название источника.";
            return false;
        }

        if (!TryParseSourceType(dto.Type, out type))
        {
            error = "Тип источника должен быть Rss, Json или Manual.";
            return false;
        }

        if (type != EventSourceType.Manual &&
            !EventSourceHelpers.TryGetHttpUri(dto.Url, out _))
        {
            error = "Для RSS/JSON источника нужен корректный http(s) URL.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dto.ConfigJson))
        {
            try
            {
                using var _ = JsonDocument.Parse(dto.ConfigJson);
            }
            catch (JsonException ex)
            {
                error = "ConfigJson содержит некорректный JSON: " + ex.Message;
                return false;
            }
        }

        return true;
    }

    private static bool TryParseSourceType(string? value, out EventSourceType type)
    {
        if (Enum.TryParse(value, true, out type))
        {
            return true;
        }

        if (string.Equals(value, "jsonapi", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "api", StringComparison.OrdinalIgnoreCase))
        {
            type = EventSourceType.Json;
            return true;
        }

        if (string.Equals(value, "atom", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "feed", StringComparison.OrdinalIgnoreCase))
        {
            type = EventSourceType.Rss;
            return true;
        }

        return false;
    }

    private static void ApplySourceValues(
        EventSource source,
        EventSourceUpsertDto dto,
        EventSourceType type)
    {
        source.Name = dto.Name.Trim();
        source.Type = type;
        source.Url = string.IsNullOrWhiteSpace(dto.Url) ? null : dto.Url.Trim();
        source.IsEnabled = dto.IsEnabled;
        source.City = string.IsNullOrWhiteSpace(dto.City) ? null : dto.City.Trim();
        source.DefaultTags = EventSourceHelpers.NormalizeTags(dto.DefaultTags);
        source.SyncIntervalMinutes = Math.Clamp(dto.SyncIntervalMinutes, 15, 10080);
        source.ConfigJson = string.IsNullOrWhiteSpace(dto.ConfigJson) ? null : dto.ConfigJson.Trim();
    }

    private static void ApplyEventValues(Event item, AdminEventUpsertDto dto)
    {
        item.Title = dto.Title.Trim();
        item.Summary = string.IsNullOrWhiteSpace(dto.Summary) ? null : dto.Summary.Trim();
        item.Description = string.IsNullOrWhiteSpace(dto.Description) ? string.Empty : dto.Description.Trim();
        item.StartsAt = ToUtc(dto.StartsAt);
        item.EndsAt = dto.EndsAt.HasValue ? ToUtc(dto.EndsAt.Value) : null;
        item.City = string.IsNullOrWhiteSpace(dto.City) ? null : dto.City.Trim();
        item.Venue = string.IsNullOrWhiteSpace(dto.Venue) ? null : dto.Venue.Trim();
        item.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
        item.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim();
        item.SourceUrl = string.IsNullOrWhiteSpace(dto.SourceUrl) ? null : dto.SourceUrl.Trim();
        item.Tags = EventSourceHelpers.NormalizeTags(dto.Tags);
        item.UpdatedAt = DateTime.UtcNow;
    }

    private static void Publish(Event item, DateTime now)
    {
        item.Status = EventStatus.Published;
        item.IsActive = true;
        item.PublishedAt ??= now;
        item.ModeratedAt = now;
        item.ModeratedBy = "admin";
        item.RejectionReason = null;
        item.UpdatedAt = now;
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime? ToUtc(DateTime? value) => value.HasValue ? ToUtc(value.Value) : null;

    private static EventSourceDto ToSourceDto(EventSource source)
    {
        return new EventSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            Type = source.Type.ToString(),
            Url = source.Url,
            IsEnabled = source.IsEnabled,
            City = source.City,
            DefaultTags = source.DefaultTags,
            SyncIntervalMinutes = source.SyncIntervalMinutes,
            ConfigJson = source.ConfigJson,
            LastSyncAt = source.LastSyncAt,
            LastSyncStatus = source.LastSyncStatus,
            LastSyncError = source.LastSyncError,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
    }

    private static AdminEventDto ToAdminDto(Event item, string? sourceName = null)
    {
        return new AdminEventDto
        {
            Id = item.Id,
            Slug = item.Slug,
            Title = item.Title,
            Summary = item.Summary,
            Description = item.Description,
            StartsAt = ToUtc(item.StartsAt),
            EndsAt = ToUtc(item.EndsAt),
            City = item.City,
            Venue = item.Venue,
            Address = item.Address,
            ImageUrl = item.ImageUrl,
            SourceUrl = item.SourceUrl,
            Tags = item.Tags,
            Status = item.Status.ToString(),
            IsActive = item.IsActive,
            SourceId = item.SourceId,
            SourceName = sourceName,
            ExternalId = item.ExternalId,
            CreatedAt = ToUtc(item.CreatedAt),
            UpdatedAt = ToUtc(item.UpdatedAt),
            PublishedAt = ToUtc(item.PublishedAt),
            RejectionReason = item.RejectionReason
        };
    }
}
