using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LP.Journal.Services.Journal;

/// <summary>
/// Нормализация текста, полученного из RSS/HTML. В теле материала сохраняется
/// только безопасное форматирование из whitelist-тегов; остальной HTML экранируется.
/// </summary>
public static class JournalTextCleaner
{
    private static readonly Regex HtmlTagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled);

    private static readonly Regex AllowedTagRegex = new(
        @"</?(b|strong|i|em|u|s|strike|p|br|ul|ol|li|blockquote|h2|h3|h4|h5|h6|code|pre|a)\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HtmlCommentRegex = new(
        "<!--.*?-->",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex DangerousBlockRegex = new(
        @"<\s*(script|style|iframe|object|embed|svg|video|audio|form)\b[^>]*>.*?<\s*/\s*\1\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex DangerousTagRegex = new(
        @"<\s*/?\s*(script|style|iframe|object|embed|svg|img|video|audio|form|input|button|meta|link|base)\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = WebUtility.HtmlDecode(HtmlTagRegex.Replace(html, " "));
        return WhitespaceRegex.Replace(text, " ").Trim();
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

    /// <summary>
    /// Нормализует тело материала, сохраняя только безопасное форматирование
    /// из whitelist-тегов. Остальной HTML экранируется.
    /// </summary>
    public static string NormalizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var sanitized = SanitizeBody(body);
        var normalized = sanitized
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        while (normalized.Contains("\n\n\n", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("\n\n\n", "\n\n", StringComparison.Ordinal);
        }

        return normalized.Trim();
    }

    private static string SanitizeBody(string body)
    {
        var decoded = WebUtility.HtmlDecode(body);
        decoded = HtmlCommentRegex.Replace(decoded, string.Empty);
        decoded = DangerousBlockRegex.Replace(decoded, string.Empty);
        decoded = DangerousTagRegex.Replace(decoded, string.Empty);

        var result = new StringBuilder(decoded.Length);
        var position = 0;
        var openTags = new List<string>();

        foreach (Match match in AllowedTagRegex.Matches(decoded))
        {
            result.Append(WebUtility.HtmlEncode(decoded[position..match.Index]));
            result.Append(RewriteTag(match, openTags));
            position = match.Index + match.Length;
        }

        result.Append(WebUtility.HtmlEncode(decoded[position..]));
        return result.ToString();
    }

    private static string RewriteTag(Match match, List<string> openTags)
    {
        var tag = match.Value;
        var name = match.Groups[1].Value.ToLowerInvariant();
        var isClosing = tag.StartsWith("</", StringComparison.OrdinalIgnoreCase);

        if (isClosing)
        {
            if (name == "br")
            {
                return string.Empty;
            }

            if (openTags.Count == 0 || openTags[^1] != name)
            {
                return string.Empty;
            }

            openTags.RemoveAt(openTags.Count - 1);
            return $"</{name}>";
        }

        if (name == "br")
        {
            return "<br>";
        }

        if (name != "a")
        {
            openTags.Add(name);
            return $"<{name}>";
        }

        var hrefMatch = Regex.Match(tag, @"\bhref\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
        var href = hrefMatch.Success
            ? hrefMatch.Groups[1].Success
                ? hrefMatch.Groups[1].Value
                : hrefMatch.Groups[2].Success ? hrefMatch.Groups[2].Value : hrefMatch.Groups[3].Value
            : string.Empty;

        if (!IsSafeHref(href))
        {
            return string.Empty;
        }

        openTags.Add(name);
        return $"<a href=\"{WebUtility.HtmlEncode(href)}\" rel=\"nofollow noopener noreferrer\" target=\"_blank\">";
    }

    private static bool IsSafeHref(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var trimmed = href.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.Contains('\\'))
        {
            return false;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase);
        }

        // Разрешаем только ссылки внутри сайта.
        return trimmed.StartsWith("/", StringComparison.Ordinal);
    }

    public static string? NormalizeTags(string? value, int maxLength = 600)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var tags = value
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
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
            .Where(value => !string.IsNullOrWhiteSpace(value))));
    }

    /// <summary>
    /// Ключевые слова источника: те же разделители, что и у тегов, через запятую.
    /// </summary>
    public static IReadOnlyList<string> ParseKeywords(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// true, если в тексте есть хотя бы одно ключевое слово. Пустой список слов
    /// означает, что фильтр не задан и подходят все статьи.
    /// </summary>
    public static bool MatchesAnyKeyword(string? text, IReadOnlyList<string> keywords)
    {
        if (keywords.Count == 0)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(text)
            && keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed.UtcDateTime
            : null;
    }
}
