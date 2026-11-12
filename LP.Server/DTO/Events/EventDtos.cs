using System.ComponentModel.DataAnnotations;

namespace LP.Server.DTO.Events;

public class PublicEventListItemDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? City { get; set; }
    public string? Venue { get; set; }
    public string? ImageUrl { get; set; }
    public string? SourceUrl { get; set; }
    public string[] Tags { get; set; } = [];
}

public sealed class PublicEventDetailDto : PublicEventListItemDto
{
    public string? Description { get; set; }
    public string? Address { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public sealed class EventSourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsEnabled { get; set; }
    public string? City { get; set; }
    public string? DefaultTags { get; set; }
    public int SyncIntervalMinutes { get; set; }
    public string? ConfigJson { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastSyncError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class EventSourceUpsertDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Type { get; set; } = "Rss";

    [StringLength(1000)]
    public string? Url { get; set; }

    public bool IsEnabled { get; set; } = true;

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(600)]
    public string? DefaultTags { get; set; }

    [Range(15, 10080)]
    public int SyncIntervalMinutes { get; set; } = 360;

    [StringLength(8000)]
    public string? ConfigJson { get; set; }
}

public sealed class AdminEventDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? City { get; set; }
    public string? Venue { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public string? SourceUrl { get; set; }
    public string? Tags { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? SourceId { get; set; }
    public string? SourceName { get; set; }
    public string? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public sealed class AdminEventUpsertDto
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(600)]
    public string? Summary { get; set; }

    [StringLength(4000)]
    public string? Description { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(250)]
    public string? Venue { get; set; }

    [StringLength(400)]
    public string? Address { get; set; }

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    [StringLength(1000)]
    public string? SourceUrl { get; set; }

    [StringLength(600)]
    public string? Tags { get; set; }

    public bool PublishImmediately { get; set; }
}

public sealed class RejectEventDto
{
    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class EventSourcePreviewItemDto
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? City { get; set; }
    public string? Venue { get; set; }
    public string? ImageUrl { get; set; }
    public string? SourceUrl { get; set; }
}

public sealed class EventSourcePreviewDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int Total { get; set; }
    public List<EventSourcePreviewItemDto> Items { get; set; } = [];
}

public sealed class EventImportResultDto
{
    public bool Success { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string? Error { get; set; }
}
