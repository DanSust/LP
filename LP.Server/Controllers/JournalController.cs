using System.Text;
using LP.Entity;
using LP.Server.DTO.Journal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LP.Server.Controllers;

/// <summary>
/// Публичный журнал Made4Love: только опубликованные и проверенные материалы.
/// </summary>
[ApiController]
[Route("Journal")]
public sealed class JournalController : ControllerBase
{
    private readonly ApplicationContext _context;
    private readonly IConfiguration _configuration;

    public JournalController(ApplicationContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("public")]
    public async Task<ActionResult<IReadOnlyList<PublicNewsListItemDto>>> PublicList(
        [FromQuery] string? topic,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.NewsItems
            .AsNoTracking()
            .Where(item => item.Status == NewsStatus.Published);

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var normalizedTopic = topic.Trim();
            query = query.Where(item => item.Topic == normalizedTopic);
        }

        var rows = await query
            .OrderByDescending(item => item.PublishedAt ?? item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                item.Id,
                item.Slug,
                item.Title,
                item.Summary,
                item.Topic,
                item.Tags,
                item.City,
                item.HeroImageUrl,
                item.AuthorName,
                item.AiGenerated,
                item.PublishedAt
            })
            .ToListAsync();

        var items = rows
            .Select(item => new PublicNewsListItemDto
            {
                Id = item.Id,
                Slug = item.Slug,
                Title = item.Title,
                Summary = item.Summary,
                Topic = item.Topic,
                Tags = SplitTags(item.Tags),
                City = item.City,
                HeroImageUrl = item.HeroImageUrl,
                AuthorName = item.AuthorName,
                AiGenerated = item.AiGenerated,
                PublishedAt = ToUtc(item.PublishedAt)
            })
            .ToList();

        return Ok(items);
    }

    [HttpGet("public/{slug}")]
    public async Task<ActionResult<PublicNewsDetailDto>> PublicDetail(string slug)
    {
        var news = await _context.NewsItems
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Slug == slug && item.Status == NewsStatus.Published);

        if (news is null)
        {
            return NotFound();
        }

        var item = new PublicNewsDetailDto
        {
            Id = news.Id,
            Slug = news.Slug,
            Title = news.Title,
            Summary = news.Summary,
            Body = news.Body,
            Topic = news.Topic,
            Tags = SplitTags(news.Tags),
            City = news.City,
            HeroImageUrl = news.HeroImageUrl,
            ImageLicense = news.ImageLicense,
            AuthorName = news.AuthorName,
            AiGenerated = news.AiGenerated,
            SourceName = news.SourceName,
            SourceUrl = news.SourceUrl,
            OriginalTitle = news.OriginalTitle,
            SourcePublishedAt = ToUtc(news.SourcePublishedAt),
            PublishedAt = ToUtc(news.PublishedAt),
            UpdatedAt = ToUtc(news.UpdatedAt)
        };

        return Ok(item);
    }

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var baseUrl = (_configuration["Journal:SiteBaseUrl"]
                       ?? _configuration["Events:SiteBaseUrl"]
                       ?? "https://made4love.ru").TrimEnd('/');

        var items = await _context.NewsItems
            .AsNoTracking()
            .Where(item => item.Status == NewsStatus.Published)
            .OrderByDescending(item => item.PublishedAt ?? item.CreatedAt)
            .Select(item => new { item.Slug, item.UpdatedAt })
            .ToListAsync();

        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        builder.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        builder.AppendLine(
            $"  <url><loc>{EscapeXml(baseUrl + "/journal")}</loc><changefreq>daily</changefreq><priority>0.8</priority></url>");

        foreach (var item in items)
        {
            builder.AppendLine("  <url>");
            builder.AppendLine($"    <loc>{EscapeXml(baseUrl + "/journal/" + item.Slug)}</loc>");
            builder.AppendLine(
                $"    <lastmod>{ToUtc(item.UpdatedAt)?.ToString("yyyy-MM-dd")}</lastmod>");
            builder.AppendLine("    <changefreq>monthly</changefreq>");
            builder.AppendLine("    <priority>0.7</priority>");
            builder.AppendLine("  </url>");
        }

        builder.AppendLine("</urlset>");

        return Content(builder.ToString(), "application/xml; charset=utf-8");
    }

    private static DateTime? ToUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static string[] SplitTags(string? tags)
    {
        return string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
