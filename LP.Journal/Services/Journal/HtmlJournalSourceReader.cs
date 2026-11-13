using System.Text.RegularExpressions;
using LP.Entity;

namespace LP.Journal.Services.Journal;

/// <summary>
/// HTML-страница со списком материалов: находит ссылки на статьи, отбрасывает
/// навигацию, рубрики, теги и служебные страницы. Текст статьи загружается
/// отдельно через <see cref="ArticleFetcher"/>.
/// </summary>
public sealed class HtmlJournalSourceReader : IJournalSourceReader
{
    private const int MaxBytes = 3 * 1024 * 1024;
    private const int MaxCandidates = 100;

    private static readonly Regex AnchorRegex = new(
        @"<a\b(?<attrs>[^>]*)>(?<text>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex HrefRegex = new(
        @"\bhref\s*=\s*(?:""(?<url>[^""]*)""|'(?<url>[^']*)'|(?<url>[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> UtilitySegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "tag", "tags", "category", "categories", "rubric", "rubrics", "theme", "themes",
        "author", "authors", "page", "search", "login", "register", "signin", "signup",
        "subscribe", "subscription", "feed", "rss", "sitemap", "privacy", "policy",
        "contacts", "contact", "about", "advert", "advertising", "terms", "cookie",
        "cookies", "help", "support", "account", "profile", "user", "users", "comments",
        "comment", "archive", "newsletter", "shop", "cart"
    };

    private static readonly HashSet<string> GenericTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "читать", "читать далее", "подробнее", "далее", "next", "more", "read more",
        "перейти", "открыть", "все материалы", "ещё", "еще"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HtmlJournalSourceReader> _logger;

    public HtmlJournalSourceReader(
        IHttpClientFactory httpClientFactory,
        ILogger<HtmlJournalSourceReader> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public JournalSourceType Type => JournalSourceType.Html;

    public async Task<IReadOnlyList<JournalSourceItem>> ReadAsync(
        JournalSource source,
        CancellationToken cancellationToken)
    {
        if (!JournalUrlGuard.TryGetPublicHttpUri(source.Url, out var sourceUri) || sourceUri is null)
        {
            throw new InvalidOperationException("Для HTML-источника нужен корректный публичный http(s) URL.");
        }

        var includePattern = CompilePattern(source.IncludePattern);

        var client = _httpClientFactory.CreateClient("journal-source");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Made4LoveJournalBot/1.0 (+https://made4love.ru; html list reader)");

        using var response = await client.GetAsync(
            sourceUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var bytes = await JournalHttp.ReadLimitedAsync(response, MaxBytes, cancellationToken);
        var html = JournalHttp.Decode(bytes, response.Content.Headers.ContentType?.CharSet);

        var baseUri = response.RequestMessage?.RequestUri ?? sourceUri;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<JournalSourceItem>();

        foreach (Match match in AnchorRegex.Matches(html))
        {
            if (result.Count >= MaxCandidates)
            {
                break;
            }

            var hrefMatch = HrefRegex.Match(match.Groups["attrs"].Value);
            if (!hrefMatch.Success)
            {
                continue;
            }

            var href = hrefMatch.Groups["url"].Value.Trim();
            if (string.IsNullOrWhiteSpace(href) ||
                href.StartsWith('#') ||
                href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Uri.TryCreate(baseUri, href, out var candidate) ||
                !JournalUrlGuard.IsAllowed(candidate) ||
                !IsSameSite(candidate, sourceUri) ||
                IsSamePage(candidate, sourceUri) ||
                !IsLikelyArticle(candidate))
            {
                continue;
            }

            if (includePattern is not null && !includePattern.IsMatch(candidate.ToString()))
            {
                continue;
            }

            var url = candidate.GetLeftPart(UriPartial.Path).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(url) || !seen.Add(url))
            {
                continue;
            }

            var title = JournalTextCleaner.Clean(match.Groups["text"].Value, 220);
            if (string.IsNullOrWhiteSpace(title) ||
                title.Length < 10 ||
                GenericTitles.Contains(title))
            {
                continue;
            }

            result.Add(new JournalSourceItem
            {
                ExternalId = url.Length <= 300 ? url : url[..300],
                Title = title,
                SourceUrl = candidate.ToString(),
                PublishedAt = null
            });
        }

        _logger.LogInformation(
            "Journal HTML source {Source} returned {Count} candidate links",
            source.Name,
            result.Count);

        return result;
    }

    private static Regex? CompilePattern(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return new Regex(
                value.Trim(),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(2));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                "Некорректное регулярное выражение в IncludePattern: " + ex.Message);
        }
    }

    private static bool IsSameSite(Uri candidate, Uri source)
    {
        var candidateHost = NormalizeHost(candidate.Host);
        var sourceHost = NormalizeHost(source.Host);

        return candidateHost.Equals(sourceHost, StringComparison.OrdinalIgnoreCase) ||
               candidateHost.EndsWith("." + sourceHost, StringComparison.OrdinalIgnoreCase) ||
               sourceHost.EndsWith("." + candidateHost, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHost(string host)
    {
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? host[4..]
            : host;
    }

    private static bool IsSamePage(Uri candidate, Uri source)
    {
        return candidate.GetLeftPart(UriPartial.Path)
            .TrimEnd('/')
            .Equals(source.GetLeftPart(UriPartial.Path).TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyArticle(Uri uri)
    {
        var path = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => UtilitySegments.Contains(segment)))
        {
            return false;
        }

        if (uri.Query.Contains("page=", StringComparison.OrdinalIgnoreCase) ||
            uri.Query.Contains("tag=", StringComparison.OrdinalIgnoreCase) ||
            uri.Query.Contains("category=", StringComparison.OrdinalIgnoreCase) ||
            uri.Query.Contains("search=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lastSegment = segments[^1];
        var dot = lastSegment.LastIndexOf('.');
        if (dot >= 0)
        {
            var extension = lastSegment[dot..].ToLowerInvariant();
            if (extension is not (".html" or ".htm" or ".shtml" or ".php" or ".asp" or ".aspx" or ".jsp"))
            {
                return false;
            }
        }

        return true;
    }
}
