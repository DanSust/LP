using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LP.Entity;

namespace LP.Server.Services.Events;

/// <summary>
/// Универсальный JSON API провайдер. Маппинг полей задаётся в ConfigJson источника,
/// поэтому один и тот же код работает с KudaGo, Timepad, собственным API и т.п.
/// </summary>
public sealed class JsonEventSourceProvider : IEventSourceProvider
{
    private static readonly string[] TitleFields = ["title", "name", "event_name", "eventName", "short_title"];
    private static readonly string[] SummaryFields = ["summary", "short_description", "shortDescription", "annotation", "lead"];
    private static readonly string[] DescriptionFields = ["description", "body", "text", "content", "details"];
    private static readonly string[] StartFields = ["startsAt", "startDate", "start_date", "start", "date", "datetime", "beginDate", "begin_date"];
    private static readonly string[] EndFields = ["endsAt", "endDate", "end_date", "end", "finishDate", "finish_date"];
    private static readonly string[] CityFields = ["city", "cityName", "city_name", "town"];
    private static readonly string[] VenueFields = ["venue", "place", "location", "placeName", "place_name"];
    private static readonly string[] AddressFields = ["address", "addressLine", "address_line", "fullAddress"];
    private static readonly string[] ImageFields = ["imageUrl", "image_url", "image", "photo", "picture", "cover", "poster"];
    private static readonly string[] UrlFields = ["sourceUrl", "source_url", "url", "link", "site_url", "siteUrl"];
    private static readonly string[] ExternalIdFields = ["externalId", "external_id", "id", "uuid", "guid", "slug"];
    private static readonly string[] TagFields = ["tags", "categories", "topics", "genres"];
    private static readonly string[] PublishedFields = ["publishedAt", "published_at", "published", "createdAt", "created_at"];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JsonEventSourceProvider> _logger;

    public JsonEventSourceProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<JsonEventSourceProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public EventSourceType Type => EventSourceType.Json;

    public async Task<IReadOnlyList<ImportedEvent>> FetchAsync(
        EventSource source,
        CancellationToken cancellationToken)
    {
        if (!EventSourceHelpers.TryGetHttpUri(source.Url, out var uri))
        {
            throw new InvalidOperationException("Для JSON-источника нужен корректный http(s) URL.");
        }

        var mapping = ParseMapping(source.ConfigJson);
        var client = _httpClientFactory.CreateClient(nameof(JsonEventSourceProvider));
        client.Timeout = TimeSpan.FromSeconds(30);

        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var result = new List<ImportedEvent>();

        foreach (var item in GetItems(document.RootElement, mapping))
        {
            var title = TextCleaner.Clean(GetString(item, mapping.Title, TitleFields), 200);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var descriptionHtml = GetString(item, mapping.Description, DescriptionFields);
            var description = TextCleaner.Clean(descriptionHtml, 4000);
            var summary = TextCleaner.Clean(GetString(item, mapping.Summary, SummaryFields), 600)
                          ?? TextCleaner.Summarize(description, 300);

            if (!TryGetDate(item, mapping.StartsAt, StartFields, out var startsAt))
            {
                if (mapping.UsePublishedAsStart == true &&
                    TryGetDate(item, null, PublishedFields, out var published))
                {
                    startsAt = published;
                }
                else
                {
                    _logger.LogDebug("JSON item without a parseable start date skipped: {Title}", title);
                    continue;
                }
            }

            DateTime? endsAt = TryGetDate(item, mapping.EndsAt, EndFields, out var parsedEnd)
                ? parsedEnd
                : null;

            var city = TextCleaner.Clean(GetString(item, mapping.City, CityFields), 120);
            var venue = TextCleaner.Clean(GetString(item, mapping.Venue, VenueFields), 250);
            var address = TextCleaner.Clean(GetString(item, mapping.Address, AddressFields), 400);

            var image = EventSourceHelpers.MakeAbsoluteUrl(
                GetImage(item, mapping.Image, descriptionHtml),
                source.Url);

            var sourceUrl = EventSourceHelpers.MakeAbsoluteUrl(
                GetString(item, mapping.Url, UrlFields),
                source.Url);

            var externalId = TextCleaner.Clean(GetString(item, mapping.ExternalId, ExternalIdFields), 300)
                             ?? BuildExternalId(title, startsAt, sourceUrl, source.Id);

            var tags = EventSourceHelpers.MergeTags(
                GetString(item, mapping.Tags, TagFields),
                source.DefaultTags);

            result.Add(new ImportedEvent
            {
                ExternalId = externalId,
                Title = title,
                Summary = summary,
                Description = description,
                StartsAt = startsAt,
                EndsAt = endsAt,
                City = city,
                Venue = venue,
                Address = address,
                ImageUrl = image,
                SourceUrl = sourceUrl,
                Tags = tags
            });
        }

        return result;
    }

    private static JsonMapping ParseMapping(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new JsonMapping();
        }

        try
        {
            return JsonSerializer.Deserialize<JsonMapping>(
                       configJson,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new JsonMapping();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("ConfigJson источника содержит некорректный JSON: " + ex.Message, ex);
        }
    }

    private static IReadOnlyList<JsonElement> GetItems(JsonElement root, JsonMapping mapping)
    {
        var node = string.IsNullOrWhiteSpace(mapping.ItemsPath)
            ? root
            : Navigate(root, mapping.ItemsPath);

        if (node.ValueKind == JsonValueKind.Array)
        {
            return node.EnumerateArray().ToList();
        }

        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "items", "events", "results", "records", "list", "data" })
            {
                if (TryGetProperty(node, name, out var child) && child.ValueKind == JsonValueKind.Array)
                {
                    return child.EnumerateArray().ToList();
                }
            }

            return [node];
        }

        return [];
    }

    private static JsonElement Navigate(JsonElement root, string path)
    {
        var current = root;

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind == JsonValueKind.Array)
            {
                if (int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                    && index >= 0
                    && index < current.GetArrayLength())
                {
                    current = current[index];
                    continue;
                }

                return default;
            }

            if (current.ValueKind != JsonValueKind.Object || !TryGetProperty(current, segment, out var next))
            {
                return default;
            }

            current = next;
        }

        return current;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(name, out value))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement item, string? field, string[] fallbacks)
    {
        if (!string.IsNullOrWhiteSpace(field))
        {
            var mapped = ToText(Navigate(item, field));
            if (!string.IsNullOrWhiteSpace(mapped))
            {
                return mapped;
            }
        }

        foreach (var fallback in fallbacks)
        {
            if (TryGetProperty(item, fallback, out var value))
            {
                var text = ToText(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string? ToText(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return value.GetString();
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return value.GetRawText();
            case JsonValueKind.Array:
                var parts = value.EnumerateArray()
                    .Select(ToText)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();
                return parts.Count == 0 ? null : string.Join(", ", parts);
            case JsonValueKind.Object:
                foreach (var name in new[] { "name", "title", "value", "text", "url", "address", "fullAddress" })
                {
                    if (TryGetProperty(value, name, out var child))
                    {
                        var text = ToText(child);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }

                return null;
            default:
                return null;
        }
    }

    private static bool TryGetDate(JsonElement item, string? field, string[] fallbacks, out DateTime utc)
    {
        utc = default;
        var text = GetString(item, field, fallbacks);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (EventDateParser.TryParse(text, out utc))
        {
            return true;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            try
            {
                utc = unix > 10_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unix).UtcDateTime
                    : DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        return false;
    }

    private static string? GetImage(JsonElement item, string? field, string? descriptionHtml)
    {
        var raw = GetString(item, field, ImageFields);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        return TextCleaner.ExtractFirstImage(descriptionHtml);
    }

    private static string BuildExternalId(string title, DateTime startsAt, string? sourceUrl, Guid sourceId)
    {
        var raw = $"{title}|{startsAt:O}|{sourceUrl}|{sourceId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class JsonMapping
    {
        public string? ItemsPath { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public string? StartsAt { get; set; }
        public string? EndsAt { get; set; }
        public string? City { get; set; }
        public string? Venue { get; set; }
        public string? Address { get; set; }
        public string? Image { get; set; }
        public string? Url { get; set; }
        public string? ExternalId { get; set; }
        public string? Tags { get; set; }
        public bool? UsePublishedAsStart { get; set; }
    }
}
