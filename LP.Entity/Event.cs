using System.ComponentModel.DataAnnotations;

namespace LP.Entity;

public enum EventStatus
{
    Draft = 0,
    Published = 1,
    Rejected = 2,
    Archived = 3
}

public class Event : BaseEntity
{
    [StringLength(220)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(600)]
    public string? Summary { get; set; }

    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndsAt { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(250)]
    public string? Venue { get; set; }

    [StringLength(400)]
    public string? Address { get; set; }

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Ссылка на оригинальную публикацию события у источника.
    /// </summary>
    [StringLength(1000)]
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Теги через запятую: город, тематика, формат встречи.
    /// </summary>
    [StringLength(600)]
    public string? Tags { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public bool IsActive { get; set; } = true;

    public Guid? SourceId { get; set; }

    public EventSource? Source { get; set; }

    /// <summary>
    /// Идентификатор события у источника. Используется для дедупликации.
    /// </summary>
    [StringLength(300)]
    public string? ExternalId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PublishedAt { get; set; }

    public DateTime? ModeratedAt { get; set; }

    [StringLength(200)]
    public string? ModeratedBy { get; set; }

    [StringLength(1000)]
    public string? RejectionReason { get; set; }
}
