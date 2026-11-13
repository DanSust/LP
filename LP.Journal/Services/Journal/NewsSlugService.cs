using LP.Common;
using LP.Entity;
using Microsoft.EntityFrameworkCore;

namespace LP.Journal.Services.Journal;

/// <summary>
/// Генерирует уникальные slug для материалов журнала.
/// </summary>
public sealed class NewsSlugService
{
    private readonly ApplicationContext _context;

    public NewsSlugService(ApplicationContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateUniqueAsync(
        string? title,
        Guid? excludeId,
        CancellationToken cancellationToken,
        ISet<string>? reserved = null)
    {
        var baseSlug = SlugHelper.Slugify(title, "article");
        var slug = baseSlug;
        var suffix = 2;

        while ((reserved?.Contains(slug) ?? false) ||
               await _context.NewsItems.AnyAsync(
                   item => item.Slug == slug && (excludeId == null || item.Id != excludeId.Value),
                   cancellationToken))
        {
            slug = $"{baseSlug}-{suffix++}";

            if (suffix > 1000)
            {
                slug = $"{baseSlug}-{Guid.NewGuid():N}";
                slug = slug[..Math.Min(slug.Length, 80)];
                break;
            }
        }

        reserved?.Add(slug);
        return slug;
    }
}
