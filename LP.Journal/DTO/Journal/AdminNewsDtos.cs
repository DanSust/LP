using System.ComponentModel.DataAnnotations;
using LP.Entity;

namespace LP.Journal.DTO.Journal;

public sealed class AdminNewsDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public string? Tags { get; set; }
    public string? City { get; set; }
    public string? HeroImageUrl { get; set; }
    public string? ImageLicense { get; set; }
    public Guid? SourceId { get; set; }
    public string? ExternalId { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public string? OriginalTitle { get; set; }
    public DateTime? SourcePublishedAt { get; set; }
    public string? AiModel { get; set; }
    public string? PromptVersion { get; set; }
    public bool AiGenerated { get; set; }
    public int? SimilarityPercent { get; set; }
    public string? AuthorName { get; set; }
    public string? EditorNotes { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool FactsChecked { get; set; }
    public bool AttributionChecked { get; set; }
    public bool ImageRightsChecked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public sealed class NewsUpsertDto
{
    [Required]
    [StringLength(220)]
    public string Title { get; set; } = string.Empty;

    [StringLength(600)]
    public string? Summary { get; set; }

    [StringLength(20000)]
    public string Body { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Topic { get; set; }

    [StringLength(600)]
    public string? Tags { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(1000)]
    public string? HeroImageUrl { get; set; }

    [StringLength(300)]
    public string? ImageLicense { get; set; }

    [StringLength(200)]
    public string? SourceName { get; set; }

    [StringLength(1000)]
    public string? SourceUrl { get; set; }

    [StringLength(300)]
    public string? OriginalTitle { get; set; }

    public DateTime? SourcePublishedAt { get; set; }

    [StringLength(200)]
    public string? AuthorName { get; set; }

    [StringLength(2000)]
    public string? EditorNotes { get; set; }

    public bool FactsChecked { get; set; }

    public bool AttributionChecked { get; set; }

    public bool ImageRightsChecked { get; set; }

    /// <summary>
    /// Пересечение с источником. Присылается клиентом после AI-черновика,
    /// сервер дополнительно проверяет порог при публикации.
    /// </summary>
    public int? SimilarityPercent { get; set; }

    [StringLength(120)]
    public string? AiModel { get; set; }

    [StringLength(60)]
    public string? PromptVersion { get; set; }

    public bool AiGenerated { get; set; }
}

public sealed class DraftFromUrlDto
{
    [Required]
    [StringLength(1000)]
    public string Url { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Topic { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(2000)]
    public string? Instructions { get; set; }
}

public sealed class DraftFromUrlResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? Body { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public string? OriginalTitle { get; set; }
    public DateTime? SourcePublishedAt { get; set; }
    public string? AiModel { get; set; }
    public string? PromptVersion { get; set; }
    public bool AiGenerated { get; set; }
    public int? SimilarityPercent { get; set; }
}

public sealed class RejectNewsDto
{
    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class JournalConfigDto
{
    public bool AiConfigured { get; set; }
    public string? AiModel { get; set; }
    public int MaxSimilarityPercent { get; set; }
    public bool CrawlEnabled { get; set; }
}

public sealed class JournalSourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public JournalSourceType Type { get; set; }
    public string? Url { get; set; }
    public bool IsEnabled { get; set; }
    public string? Topic { get; set; }
    public string? City { get; set; }
    public string? DefaultTags { get; set; }
    public int SyncIntervalMinutes { get; set; }
    public int MaxItemsPerSync { get; set; }
    public string? IncludePattern { get; set; }
    public bool AiDrafts { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastSyncError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class JournalSourceUpsertDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public JournalSourceType Type { get; set; } = JournalSourceType.Rss;

    [StringLength(1000)]
    public string? Url { get; set; }

    public bool IsEnabled { get; set; } = true;

    [StringLength(120)]
    public string? Topic { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(600)]
    public string? DefaultTags { get; set; }

    public int SyncIntervalMinutes { get; set; } = 180;

    public int MaxItemsPerSync { get; set; } = 10;

    [StringLength(2000)]
    public string? IncludePattern { get; set; }

    public bool AiDrafts { get; set; } = true;
}

public sealed class JournalSourceTestItemDto
{
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public sealed class JournalSourceTestResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int ItemCount { get; set; }
    public IReadOnlyList<JournalSourceTestItemDto> Items { get; set; } = [];
}

public sealed class JournalSyncResultDto
{
    public bool Success { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
}
