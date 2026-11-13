using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LP.Journal.Services.Journal;

/// <summary>
/// Генератор черновиков через OpenAI-совместимый API.
/// Подходит для OpenAI, OpenRouter, DeepSeek, Yandex Cloud и других
/// провайдеров с тем же форматом /chat/completions.
/// </summary>
public sealed class OpenAiNewsDraftGenerator : INewsDraftGenerator
{
    public const string CurrentPromptVersion = "journal-draft-v1";

    private const string SystemPrompt = """
        Ты — редактор журнала Made4Love. Сайт помогает людям строить серьёзные отношения.
        Пиши на русском языке, спокойно и по делу, без канцелярита и кликбейта.

        Жёсткие правила:
        1. Не копируй формулировки источника. Максимум — одна короткая цитата в кавычках.
        2. Не выдумывай факты, цифры, имена, даты и цитаты. Если факта нет в источнике — не пиши его.
        3. Не упоминай, что текст создан ИИ, и не обращайся к читателю от лица ассистента.
        4. Объём тела статьи — 400–700 слов, 4–7 абзацев. Абзацы разделяй пустой строкой.
        5. Без HTML, без markdown, без списков и заголовков внутри body.
        6. Заголовок — до 120 символов, цепляющий, но без обмана.
        7. Summary — до 300 символов, одно-два предложения.
        8. Добавь собственный угол: что это значит для отношений, выбора города или досуга.

        Верни строго JSON без пояснений:
        {"title": "...", "summary": "...", "body": "..."}
        """;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiNewsDraftGenerator> _logger;

    public OpenAiNewsDraftGenerator(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAiNewsDraftGenerator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured =>
        _configuration.GetValue("Journal:AiEnabled", false) &&
        !string.IsNullOrWhiteSpace(_configuration["Journal:AiApiKey"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Journal:AiModel"]) &&
        !string.IsNullOrWhiteSpace(_configuration["Journal:AiBaseUrl"]);

    public string? Model => _configuration["Journal:AiModel"];

    public async Task<NewsDraftGenerationResult> GenerateAsync(
        NewsDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return Failure("AI-генератор не настроен. Заполните Journal:AiApiKey, AiBaseUrl и AiModel.");
        }

        var baseUrl = _configuration["Journal:AiBaseUrl"]!.TrimEnd('/');
        var apiKey = _configuration["Journal:AiApiKey"]!;
        var model = _configuration["Journal:AiModel"]!;

        var payload = new
        {
            model,
            temperature = 0.4,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = BuildUserPrompt(request) }
            },
            response_format = new { type = "json_object" }
        };

        var client = _httpClientFactory.CreateClient("journal-ai");
        client.Timeout = TimeSpan.FromSeconds(120);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        HttpResponseMessage response;

        try
        {
            response = await client.PostAsync(
                $"{baseUrl}/chat/completions",
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"),
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Journal AI request failed");
            return Failure("Не удалось связаться с AI-провайдером. Проверьте Journal:AiBaseUrl и сеть.");
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Journal AI returned {Status}: {Body}",
                    (int)response.StatusCode,
                    raw.Length > 500 ? raw[..500] : raw);

                return Failure($"AI-провайдер ответил {(int)response.StatusCode}. Проверьте ключ и модель.");
            }

            var content = ExtractContent(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                return Failure("AI-провайдер вернул пустой ответ.");
            }

            var parsed = ParseDraft(content);
            if (parsed is null)
            {
                return Failure("Не удалось разобрать JSON от AI. Попробуйте ещё раз или создайте черновик вручную.");
            }

            var body = NormalizeBody(parsed.Body);
            if (body.Length < 400)
            {
                return Failure("AI вернул слишком короткий текст. Попробуйте ещё раз или напишите черновик вручную.");
            }

            return new NewsDraftGenerationResult(
                true,
                null,
                Trim(parsed.Title, 220),
                Trim(parsed.Summary, 600),
                body,
                model,
                CurrentPromptVersion);
        }
    }

    private static string BuildUserPrompt(NewsDraftRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Источник:");
        builder.AppendLine($"URL: {request.SourceUrl}");

        if (!string.IsNullOrWhiteSpace(request.SourceName))
        {
            builder.AppendLine($"Издание: {request.SourceName}");
        }

        if (!string.IsNullOrWhiteSpace(request.SourceTitle))
        {
            builder.AppendLine($"Заголовок источника: {request.SourceTitle}");
        }

        if (!string.IsNullOrWhiteSpace(request.Topic))
        {
            builder.AppendLine($"Тема журнала: {request.Topic}");
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            builder.AppendLine($"Город: {request.City}");
        }

        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            builder.AppendLine($"Пожелания редактора: {request.Instructions}");
        }

        builder.AppendLine();
        builder.AppendLine("Текст источника (используй только как источник фактов):");
        builder.AppendLine(request.SourceText.Length > 12000
            ? request.SourceText[..12000]
            : request.SourceText);

        return builder.ToString();
    }

    private static string? ExtractContent(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                if (message.TryGetProperty("content", out var content))
                {
                    return content.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Ниже вернём null — вызывающий код покажет понятную ошибку.
        }

        return null;
    }

    private static DraftPayload? ParseDraft(string content)
    {
        var json = content.Trim();

        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = json.IndexOf('\n');
            var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
            {
                json = json[(firstNewline + 1)..lastFence];
            }
        }

        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        json = json[start..(end + 1)];

        try
        {
            return JsonSerializer.Deserialize<DraftPayload>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var normalized = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        while (normalized.Contains("\n\n\n", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);
        }

        return normalized.Trim();
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd();
    }

    private static NewsDraftGenerationResult Failure(string error)
    {
        return new NewsDraftGenerationResult(false, error, null, null, null, null, null);
    }

    private sealed class DraftPayload
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }
}
