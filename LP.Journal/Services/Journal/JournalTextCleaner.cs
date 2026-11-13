using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace LP.Journal.Services.Journal;

/// <summary>
/// Нормализация текста, полученного из RSS/HTML. Храним только обычный текст:
/// без HTML-тегов и лишних пробелов.
/// </summary>
public static class JournalTextCleaner
{
    private static readonly Regex HtmlTagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled);

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

    public static string NormalizeBody(string? body)
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
