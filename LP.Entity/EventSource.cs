using System.ComponentModel.DataAnnotations;

namespace LP.Entity;

public enum EventSourceType
{
    Rss = 0,
    Json = 1,
    Manual = 2
}

/// <summary>
/// Настраиваемый источник реальных событий: RSS/Atom, JSON API или ручной ввод.
/// </summary>
public class EventSource : BaseEntity
{
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public EventSourceType Type { get; set; } = EventSourceType.Rss;

    [StringLength(1000)]
    public string? Url { get; set; }

    public bool IsEnabled { get; set; } = true;

    [StringLength(120)]
    public string? City { get; set; }

    /// <summary>
    /// Теги по умолчанию, добавляются ко всем событиям источника.
    /// </summary>
    [StringLength(600)]
    public string? DefaultTags { get; set; }

    /// <summary>
    /// Как часто опрашивать источник. Минимум 15 минут.
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 360;

    /// <summary>
    /// Настройки маппинга для JSON API. Для RSS не используется.
    /// </summary>
    [StringLength(8000)]
    public string? ConfigJson { get; set; }

    public DateTime? LastSyncAt { get; set; }

    [StringLength(500)]
    public string? LastSyncStatus { get; set; }

    [StringLength(2000)]
    public string? LastSyncError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
