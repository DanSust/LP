using System.Xml.Linq;
using LP.Entity;

namespace LP.Server.Services.Events;

/// <summary>
/// RSS 2.0 / Atom / RDF-подобные ленты. Провайдер намеренно не привязан
/// к конкретному сервису: даты и место ищутся по распространённым именам тегов.
/// </summary>
public sealed class RssEventSourceProvider : IEventSourceProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RssEventSourceProvider> _logger;

    public RssEventSourceProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<RssEventSourceProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public EventSourceType Type => EventSourceType.Rss;

    public async Task<IReadOnlyList<ImportedEvent>> FetchAsync(
        EventSource source,
        CancellationToken cancellationToken)
    {
        if (!EventSourceHelpers.TryGetHttpUri(source.Url, out var uri))
        {
            throw new InvalidOperationException("Для RSS-источника нужен корректный http(s) URL.");
        }

        var client = _httpClientFactory.CreateClient(nameof(RssEventSourceProvider));
        client.Timeout = TimeSpan.FromSeconds(30);

        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);

        var items = document
            .Descendants()
            .Where(element => element.Name.LocalName is "item" or "entry")
            .ToList();

        var result = new List<ImportedEvent>(items.Count);

        foreach (var item in items)
        {
            var title = TextCleaner.Clean(GetValue(item, "title"), 200);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var descriptionHtml = GetValue(item, "description", "summary", "content", "encoded");
            var description = TextCleaner.Clean(descriptionHtml, 4000);
            var link = GetLink(item);

            var startRaw = GetValue(
                item,
                "startdate", "startDate", "start", "dtstart", "dtStart",
                "eventstart", "eventStart", "event_date", "date",
                "pubdate", "published", "updated");

            if (!EventDateParser.TryParse(startRaw, out var startsAt))
            {
                _logger.LogDebug("RSS item without a parseable date skipped: {Title}", title);
                continue;
            }

            var endRaw = GetValue(item, "enddate", "endDate", "end", "dtend", "dtEnd", "eventend", "eventEnd");
            DateTime? endsAt = EventDateParser.TryParse(endRaw, out var parsedEnd) ? parsedEnd : null;

            var city = TextCleaner.Clean(GetValue(item, "city", "eventcity", "eventCity"), 120);
            var venue = TextCleaner.Clean(GetValue(item, "venue", "place", "location", "eventvenue", "eventVenue"), 250);
            var address = TextCleaner.Clean(GetValue(item, "address", "eventaddress", "eventAddress"), 400);

            var sourceTags = GetCategories(item);
            var image = GetImage(item, descriptionHtml);
            var externalId = TextCleaner.Clean(
                GetValue(item, "guid", "id", "identifier") ?? link,
                300);

            result.Add(new ImportedEvent
            {
                ExternalId = externalId,
                Title = title,
                Summary = TextCleaner.Summarize(description, 300),
                Description = description,
                StartsAt = startsAt,
                EndsAt = endsAt,
                City = city,
                Venue = venue,
                Address = address,
                ImageUrl = image,
                SourceUrl = link,
                Tags = EventSourceHelpers.MergeTags(sourceTags, source.DefaultTags)
            });
        }

        return result;
    }

    private static string? GetValue(XElement parent, params string[] names)
    {
        foreach (var name in names)
        {
            var element = parent.Elements().FirstOrDefault(
                child => string.Equals(child.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));

            if (element is null)
            {
                continue;
            }

            var value = element.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? GetLink(XElement item)
    {
        var link = item.Elements().FirstOrDefault(
            element => string.Equals(element.Name.LocalName, "link", StringComparison.OrdinalIgnoreCase));

        if (link is not null)
        {
            var href = (string?)link.Attribute("href");
            if (!string.IsNullOrWhiteSpace(href))
            {
                return href.Trim();
            }

            if (!string.IsNullOrWhiteSpace(link.Value))
            {
                return link.Value.Trim();
            }
        }

        var guid = GetValue(item, "guid", "id", "identifier");
        return EventSourceHelpers.TryGetHttpUri(guid, out var uri) ? uri!.ToString() : null;
    }

    private static string? GetCategories(XElement item)
    {
        var categories = item
            .Elements()
            .Where(element => string.Equals(element.Name.LocalName, "category", StringComparison.OrdinalIgnoreCase))
            .Select(element => (string?)element.Attribute("term") ?? element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        var tags = GetValue(item, "tags", "tag");
        if (!string.IsNullOrWhiteSpace(tags))
        {
            categories.Add(tags);
        }

        return EventSourceHelpers.NormalizeTags(string.Join(", ", categories));
    }

    private static string? GetImage(XElement item, string? descriptionHtml)
    {
        foreach (var element in item.Descendants())
        {
            var localName = element.Name.LocalName.ToLowerInvariant();
            var url = (string?)element.Attribute("url");
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var type = (string?)element.Attribute("type");
            var medium = (string?)element.Attribute("medium");
            var isImage = localName == "thumbnail"
                          || (type?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false)
                          || string.Equals(medium, "image", StringComparison.OrdinalIgnoreCase)
                          || LooksLikeImageUrl(url);

            if (isImage)
            {
                return url.Trim();
            }
        }

        return TextCleaner.ExtractFirstImage(descriptionHtml);
    }

    private static bool LooksLikeImageUrl(string value)
    {
        var path = value.Split('?', '#')[0];
        return path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".avif", StringComparison.OrdinalIgnoreCase);
    }
}
