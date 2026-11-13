using System.Xml.Linq;
using LP.Entity;

namespace LP.Journal.Services.Journal;

/// <summary>
/// RSS 2.0 / Atom / RDF-подобные ленты. Читатель не привязан к конкретному
/// изданию: заголовок, ссылка, дата и описание ищутся по распространённым тегам.
/// </summary>
public sealed class RssJournalSourceReader : IJournalSourceReader
{
    private const int MaxBytes = 5 * 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RssJournalSourceReader> _logger;

    public RssJournalSourceReader(
        IHttpClientFactory httpClientFactory,
        ILogger<RssJournalSourceReader> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public JournalSourceType Type => JournalSourceType.Rss;

    public async Task<IReadOnlyList<JournalSourceItem>> ReadAsync(
        JournalSource source,
        CancellationToken cancellationToken)
    {
        if (!JournalUrlGuard.TryGetPublicHttpUri(source.Url, out var uri) || uri is null)
        {
            throw new InvalidOperationException("Для RSS-источника нужен корректный публичный http(s) URL.");
        }

        var client = _httpClientFactory.CreateClient("journal-source");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Made4LoveJournalBot/1.0 (+https://made4love.ru; feed reader)");

        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await JournalHttp.ReadLimitedAsync(response, MaxBytes, cancellationToken);

        using var stream = new MemoryStream(bytes);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);

        var nodes = document
            .Descendants()
            .Where(element => element.Name.LocalName is "item" or "entry")
            .ToList();

        var result = new List<JournalSourceItem>(nodes.Count);

        foreach (var node in nodes)
        {
            var title = JournalTextCleaner.Clean(GetValue(node, "title"), 220);
            var link = ResolveUrl(GetLink(node), uri);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link) ||
                !JournalUrlGuard.TryGetPublicHttpUri(link, out var publicLink) || publicLink is null)
            {
                continue;
            }

            var descriptionHtml = GetValue(node, "description", "summary", "content", "encoded");
            var content = JournalTextCleaner.Clean(descriptionHtml, 16000);
            var externalId = JournalTextCleaner.Clean(
                GetValue(node, "guid", "id", "identifier") ?? link,
                300);

            result.Add(new JournalSourceItem
            {
                ExternalId = externalId,
                Title = title,
                Summary = JournalTextCleaner.Summarize(descriptionHtml, 600),
                Content = content,
                SourceUrl = publicLink.ToString(),
                PublishedAt = JournalTextCleaner.ParseDate(
                    GetValue(node, "pubdate", "published", "updated", "date", "created")),
                Tags = GetCategories(node)
            });
        }

        _logger.LogInformation(
            "Journal RSS source {Source} returned {Count} items",
            source.Name,
            result.Count);

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

        return JournalUrlGuard.TryGetPublicHttpUri(
            GetValue(item, "guid", "id", "identifier"),
            out var guidUri)
            ? guidUri!.ToString()
            : null;
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

        return JournalTextCleaner.NormalizeTags(string.Join(", ", categories));
    }

    private static string? ResolveUrl(string? value, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        return Uri.TryCreate(baseUri, value.Trim(), out var combined)
            ? combined.ToString()
            : null;
    }
}
