using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace LP.Journal.Services.Journal;

public sealed record FetchedArticle(
    string SourceUrl,
    string? SourceName,
    string? Title,
    string Text,
    DateTime? PublishedAt);

/// <summary>
/// Загружает страницу-источник и извлекает из неё заголовок, дату и текст.
/// Изображения намеренно не забираются: права на них нужно подтверждать отдельно.
/// </summary>
public sealed class ArticleFetcher
{
    private const int MaxBytes = 2 * 1024 * 1024;
    private const int MaxTextLength = 16000;

    private static readonly Regex ScriptStyleRegex = new(
        @"<(script|style|noscript|svg|iframe|form)\b[^>]*>.*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ChromeRegex = new(
        @"<(nav|header|footer|aside)\b[^>]*>.*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex CommentRegex = new(
        @"<!--.*?-->",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex BlockTagRegex = new(
        @"</?(p|div|br|li|h[1-6]|article|section|blockquote|tr|td|table|ul|ol|figure|figcaption)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(
        @"<[^>]+>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex SpacesRegex = new(
        @"[ \t\u00A0]+",
        RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ArticleFetcher> _logger;

    public ArticleFetcher(IHttpClientFactory httpClientFactory, ILogger<ArticleFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<FetchedArticle> FetchAsync(string rawUrl, CancellationToken cancellationToken)
    {
        if (!JournalUrlGuard.TryGetPublicHttpUri(rawUrl, out var uri) || uri is null)
        {
            throw new InvalidOperationException(
                "Нужна публичная ссылка http(s). Локальные и служебные адреса запрещены.");
        }

        var client = _httpClientFactory.CreateClient("journal-fetch");
        client.Timeout = TimeSpan.FromSeconds(25);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Made4LoveJournalBot/1.0 (+https://made4love.ru; editor review)");

        using var response = await client.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaxBytes)
        {
            throw new InvalidOperationException("Страница источника слишком большая.");
        }

        var bytes = await ReadLimitedAsync(response, cancellationToken);
        var html = Decode(bytes, response.Content.Headers.ContentType?.CharSet);

        var title = ExtractTitle(html);
        var siteName = ExtractMeta(html, "og:site_name");
        var publishedAt = ParseDate(
            ExtractMeta(html, "article:published_time")
            ?? ExtractMeta(html, "datepublished")
            ?? ExtractMeta(html, "pubdate"));

        var text = ExtractText(html);

        if (text.Length < 200)
        {
            throw new InvalidOperationException(
                "Не удалось извлечь текст статьи. Возможно, страница отдаёт контент только через JavaScript.");
        }

        _logger.LogInformation("Journal source fetched: {Url}", uri);

        return new FetchedArticle(
            uri.ToString(),
            siteName,
            title,
            text,
            publishedAt);
    }

    private static async Task<byte[]> ReadLimitedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > MaxBytes)
            {
                throw new InvalidOperationException("Страница источника слишком большая.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static string Decode(byte[] bytes, string? charset)
    {
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset.Trim('"', ' ')).GetString(bytes);
            }
            catch (ArgumentException)
            {
                // Неизвестная кодировка — используем UTF-8.
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static string ExtractText(string html)
    {
        var cleaned = ScriptStyleRegex.Replace(html, " ");
        cleaned = ChromeRegex.Replace(cleaned, " ");
        cleaned = CommentRegex.Replace(cleaned, " ");
        cleaned = BlockTagRegex.Replace(cleaned, "\n");
        cleaned = TagRegex.Replace(cleaned, " ");
        cleaned = WebUtility.HtmlDecode(cleaned);

        var lines = cleaned
            .Split('\n')
            .Select(line => SpacesRegex.Replace(line, " ").Trim())
            .Where(line => line.Length > 0)
            .ToList();

        var text = string.Join("\n\n", lines);

        if (text.Length > MaxTextLength)
        {
            text = text[..MaxTextLength];
        }

        return text;
    }

    private static string? ExtractTitle(string html)
    {
        var ogTitle = ExtractMeta(html, "og:title");
        if (!string.IsNullOrWhiteSpace(ogTitle))
        {
            return ogTitle.Trim();
        }

        var match = Regex.Match(
            html,
            @"<title[^>]*>(.*?)</title>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success
            ? WebUtility.HtmlDecode(TagRegex.Replace(match.Groups[1].Value, " ")).Trim()
            : null;
    }

    private static string? ExtractMeta(string html, string name)
    {
        var escaped = Regex.Escape(name);

        var patterns = new[]
        {
            $@"<meta[^>]+(?:property|name)\s*=\s*[""']{escaped}[""'][^>]+content\s*=\s*[""'](?<value>.*?)[""']",
            $@"<meta[^>]+content\s*=\s*[""'](?<value>.*?)[""'][^>]+(?:property|name)\s*=\s*[""']{escaped}[""']"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(
                html,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success)
            {
                var value = WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
                if (value.Length > 0)
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed.UtcDateTime
            : null;
    }
}
