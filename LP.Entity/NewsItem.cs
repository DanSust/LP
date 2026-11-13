using System.ComponentModel.DataAnnotations;

namespace LP.Entity;

public enum NewsStatus
{
    Draft = 0,
    Published = 1,
    Rejected = 2,
    Archived = 3
}

/// <summary>
/// Курируемый материал журнала Made4Love. Черновик может быть подготовлен
/// вручную или с помощью ИИ, но публикуется только после проверки редактором.
/// </summary>
public class NewsItem : BaseEntity
{
    [StringLength(220)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(220)]
    public string Title { get; set; } = string.Empty;

    [StringLength(600)]
    public string? Summary { get; set; }

    /// <summary>
    /// Текст материала: абзацы разделены пустой строкой.
    /// Хранится как обычный текст, без HTML.
    /// </summary>
    [StringLength(20000)]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Основная тема: отношения, свидания, города, события и т.п.
    /// </summary>
    [StringLength(120)]
    public string? Topic { get; set; }

    [StringLength(600)]
    public string? Tags { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    [StringLength(1000)]
    public string? HeroImageUrl { get; set; }

    /// <summary>
    /// Подтверждение прав на изображение: "своё", "пресс-служба", лицензия и т.п.
    /// </summary>
    [StringLength(300)]
    public string? ImageLicense { get; set; }

    /// <summary>
    /// Источник, из которого краулер создал черновик. NULL для ручных материалов.
    /// </summary>
    public Guid? SourceId { get; set; }

    public JournalSource? Source { get; set; }

    /// <summary>
    /// Идентификатор материала внутри источника (guid из RSS, canonical URL и т.п.).
    /// Используется для дедупликации.
    /// </summary>
    [StringLength(300)]
    public string? ExternalId { get; set; }

    [StringLength(200)]
    public string? SourceName { get; set; }

    [StringLength(1000)]
    public string? SourceUrl { get; set; }

    [StringLength(300)]
    public string? OriginalTitle { get; set; }

    public DateTime? SourcePublishedAt { get; set; }

    [StringLength(120)]
    public string? AiModel { get; set; }

    [StringLength(60)]
    public string? PromptVersion { get; set; }

    public bool AiGenerated { get; set; }

    /// <summary>
    /// Пересечение черновика с исходным текстом, в процентах.
    /// NULL — проверка не выполнялась (например, материал написан полностью вручную).
    /// </summary>
    public int? SimilarityPercent { get; set; }

    [StringLength(200)]
    public string? AuthorName { get; set; }

    [StringLength(2000)]
    public string? EditorNotes { get; set; }

    public NewsStatus Status { get; set; } = NewsStatus.Draft;

    public bool FactsChecked { get; set; }

    public bool AttributionChecked { get; set; }

    public bool ImageRightsChecked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PublishedAt { get; set; }

    public DateTime? ModeratedAt { get; set; }

    [StringLength(200)]
    public string? ModeratedBy { get; set; }

    [StringLength(1000)]
    public string? RejectionReason { get; set; }
}
