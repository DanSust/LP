using LP.Common;
using LP.Entity;
using Microsoft.EntityFrameworkCore;

namespace LP.Server.Services.Events;

/// <summary>
/// Генерирует человекочитаемые URL для событий и следит за уникальностью.
/// </summary>
public sealed class EventSlugService
{
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
        return SlugHelper.Slugify(value, "event");
    }
}
