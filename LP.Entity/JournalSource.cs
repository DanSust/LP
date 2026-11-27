using System.ComponentModel.DataAnnotations;

namespace LP.Entity;

public enum JournalSourceType
{
    Rss = 0,
    Html = 1
}

/// <summary>
/// Настраиваемый источник поводов для журнала: RSS/Atom-лента или HTML-страница
/// со списком материалов. Сервис LP.Journal периодически обходит источники
/// и создаёт только черновики — автопубликации нет.
/// </summary>
public class JournalSource : BaseEntity
{
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public JournalSourceType Type { get; set; } = JournalSourceType.Rss;

    [StringLength(1000)]
    public string? Url { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Тема по умолчанию для черновиков источника.
    /// </summary>
    [StringLength(120)]
    public string? Topic { get; set; }

    [StringLength(120)]
    public string? City { get; set; }

    /// <summary>
    /// Ключевые слова через запятую для отбора статей источника: черновик
    /// создаётся только если в заголовке, описании или тексте статьи есть
    /// хотя бы одно из слов. Пустое значение — фильтр не применяется.
    /// </summary>
    [StringLength(600)]
    public string? DefaultTags { get; set; }

    /// <summary>
    /// Как часто опрашивать источник. Минимум 15 минут.
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 180;

    /// <summary>
    /// Сколько новых материалов забирать из источника за один проход.
    /// </summary>
    public int MaxItemsPerSync { get; set; } = 10;

    /// <summary>
    /// Необязательное регулярное выражение для ссылок HTML-источника.
    /// Позволяет ограничить обход только статьями нужного раздела.
    /// </summary>
    [StringLength(2000)]
    public string? IncludePattern { get; set; }

    /// <summary>
    /// Разрешить AI-переписывание материалов этого источника.
    /// Если AI не настроен, черновик всё равно создаётся — пустой, для ручной редактуры.
    /// </summary>
    public bool AiDrafts { get; set; } = true;

    public DateTime? LastSyncAt { get; set; }

    [StringLength(500)]
    public string? LastSyncStatus { get; set; }

    [StringLength(2000)]
    public string? LastSyncError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<NewsItem> Items { get; set; } = new List<NewsItem>();
}
