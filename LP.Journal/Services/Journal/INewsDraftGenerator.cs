namespace LP.Journal.Services.Journal;

public sealed record NewsDraftRequest(
    string SourceUrl,
    string? SourceName,
    string? SourceTitle,
    string SourceText,
    string? Topic,
    string? City,
    string? Instructions);

public sealed record NewsDraftGenerationResult(
    bool Success,
    string? Error,
    string? Title,
    string? Summary,
    string? Body,
    string? Model,
    string? PromptVersion);

/// <summary>
/// Генератор черновика журнала. Реализация может быть отключена конфигурацией —
/// в этом случае редактор создаёт материал вручную.
/// </summary>
public interface INewsDraftGenerator
{
    bool IsConfigured { get; }

    string? Model { get; }

    Task<NewsDraftGenerationResult> GenerateAsync(
        NewsDraftRequest request,
        CancellationToken cancellationToken);
}
