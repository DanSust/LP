namespace LP.Server.Services.Events;

/// <summary>
/// Нормализованное событие, полученное из внешнего источника.
/// </summary>
public sealed record ImportedEvent
{
    public string? ExternalId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public string? City { get; init; }
    public string? Venue { get; init; }
    public string? Address { get; init; }
    public string? ImageUrl { get; init; }
    public string? SourceUrl { get; init; }
    public string? Tags { get; init; }
}
