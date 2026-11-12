using System.Globalization;
using System.Text;
using LP.Entity;
using Microsoft.EntityFrameworkCore;

namespace LP.Server.Services.Events;

/// <summary>
/// Генерирует человекочитаемые URL для событий и следит за уникальностью.
/// </summary>
public sealed class EventSlugService
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

    private readonly ApplicationContext _context;

    public EventSlugService(ApplicationContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateUniqueAsync(
        string? title,
        Guid? excludeId,
        CancellationToken cancellationToken,
        ISet<string>? reserved = null)
    {
        var baseSlug = Slugify(title);
        var slug = baseSlug;
        var suffix = 2;

        while ((reserved?.Contains(slug) ?? false) ||
               await _context.Events.AnyAsync(
                   e => e.Slug == slug && (excludeId == null || e.Id != excludeId.Value),
                   cancellationToken))
        {
            slug = $"{baseSlug}-{suffix++}";

            if (suffix > 1000)
            {
                slug = $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(baseSlug.Length + 33, 80)];
                break;
            }
        }

        reserved?.Add(slug);
        return slug;
    }

    public static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "event";
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

        return string.IsNullOrWhiteSpace(slug) ? "event" : slug;
    }
}
