using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace LP.Server.Services.Events;

public static partial class TextCleaner
{
    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<img[^>]+src\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FirstImageRegex();

    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = WebUtility.HtmlDecode(HtmlTagRegex().Replace(html, " "));
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    public static string? Clean(string? value, int maxLength)
    {
        var text = ToPlainText(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd();
    }

    public static string? Summarize(string? value, int maxLength = 300)
    {
        var text = ToPlainText(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        var cut = text[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > maxLength / 2)
        {
            cut = cut[..lastSpace];
        }

        return cut.TrimEnd(' ', ',', '.', ';', ':') + "…";
    }

    public static string? ExtractFirstImage(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var match = FirstImageRegex().Match(html);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }
}

public static class EventSourceHelpers
{
    public static string? NormalizeTags(string? value, int maxLength = 600)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var tags = value
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tags.Count == 0)
        {
            return null;
        }

        var result = string.Join(", ", tags);
        return result.Length <= maxLength ? result : result[..maxLength].TrimEnd(' ', ',');
    }

    public static string? MergeTags(string? sourceTags, string? defaultTags)
    {
        return NormalizeTags(string.Join(", ", new[] { sourceTags, defaultTags }
            .Where(v => !string.IsNullOrWhiteSpace(v))));
    }

    public static bool TryGetHttpUri(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static string? MakeAbsoluteUrl(string? value, string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TryGetHttpUri(value, out var absolute))
        {
            return absolute!.ToString();
        }

        if (TryGetHttpUri(baseUrl, out var baseUri) &&
            Uri.TryCreate(baseUri, value, out var combined))
        {
            return combined.ToString();
        }

        return null;
    }
}

public static class EventDateParser
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd",
        "dd.MM.yyyy HH:mm",
        "dd.MM.yyyy",
        "ddd, dd MMM yyyy HH:mm:ss K",
        "ddd, dd MMM yyyy HH:mm:ss 'GMT'"
    ];

    public static bool TryParse(string? value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out var dto))
        {
            utc = dto.UtcDateTime;
            return true;
        }

        if (DateTime.TryParseExact(
                text,
                Formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                out var dt))
        {
            utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return true;
        }

        return false;
    }
}
