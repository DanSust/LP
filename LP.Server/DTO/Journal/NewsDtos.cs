namespace LP.Server.DTO.Journal;

public class PublicNewsListItemDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Topic { get; set; }
    public string[] Tags { get; set; } = [];
    public string? City { get; set; }
    public string? HeroImageUrl { get; set; }
    public string? AuthorName { get; set; }
    public bool AiGenerated { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public sealed class PublicNewsDetailDto : PublicNewsListItemDto
{
    public string Body { get; set; } = string.Empty;
    public string? ImageLicense { get; set; }
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public string? OriginalTitle { get; set; }
    public DateTime? SourcePublishedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
