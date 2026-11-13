using System.Globalization;
using System.Text;

namespace LP.Common;

/// <summary>
/// Транслитерация заголовков в человекочитаемые URL.
/// </summary>
public static class SlugHelper
{
    private static readonly Dictionary<char, string> Transliteration = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
        ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
        ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
        ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
        ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch",
        ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
        ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
        ['і'] = "i", ['ї'] = "i", ['є'] = "e", ['ґ'] = "g",
        ['ў'] = "u"
    };

    public static string Slugify(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder(value.Length * 2);

        foreach (var rawChar in value.Trim().ToLowerInvariant())
        {
            if (Transliteration.TryGetValue(rawChar, out var replacement))
            {
                builder.Append(replacement);
                continue;
            }

            if (char.IsLetterOrDigit(rawChar) && rawChar <= 127)
            {
                builder.Append(rawChar);
                continue;
            }

            if (rawChar is ' ' or '-' or '_' or '/' or '\\' or '.' or ',' or ':' or ';' or '!')
            {
                builder.Append('-');
            }
        }

        var slug = builder
            .ToString()
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c))
            .ToString();

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');

        if (slug.Length > 80)
        {
            slug = slug[..80].TrimEnd('-');
        }

        return string.IsNullOrWhiteSpace(slug) ? fallback : slug;
    }
}
