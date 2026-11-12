using System.Text;
using LP.Entity;
using LP.Server.DTO.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EventsController : BaseAuthController
    {
        private readonly IConfiguration _configuration;

        public EventsController(ApplicationContext context, IConfiguration configuration)
            : base(context)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Публичный список опубликованных событий. Доступен без авторизации.
        /// </summary>
        [HttpGet("public")]
        public async Task<ActionResult<IReadOnlyList<PublicEventListItemDto>>> PublicList(
            [FromQuery] string? city,
            [FromQuery] string? tag,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var now = DateTime.UtcNow;
            var query = _context.Events
                .AsNoTracking()
                .Where(e => e.Status == EventStatus.Published
                            && e.IsActive
                            && (e.EndsAt ?? e.StartsAt) >= now.AddDays(-1));

            if (!string.IsNullOrWhiteSpace(city))
            {
                var normalizedCity = city.Trim();
                query = query.Where(e => e.City == normalizedCity);
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                var normalizedTag = tag.Trim();
                query = query.Where(e => e.Tags != null && e.Tags.Contains(normalizedTag));
            }

            if (from.HasValue)
            {
                var fromUtc = ToUtc(from.Value);
                query = query.Where(e => e.StartsAt >= fromUtc);
            }

            if (to.HasValue)
            {
                var toUtc = ToUtc(to.Value);
                query = query.Where(e => e.StartsAt <= toUtc);
            }

            var rows = await query
                .OrderBy(e => e.StartsAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new
                {
                    e.Id,
                    e.Slug,
                    e.Title,
                    e.Summary,
                    e.StartsAt,
                    e.EndsAt,
                    e.City,
                    e.Venue,
                    e.ImageUrl,
                    e.SourceUrl,
                    e.Tags
                })
                .ToListAsync();

            var events = rows
                .Select(e => new PublicEventListItemDto
                {
                    Id = e.Id,
                    Slug = e.Slug,
                    Title = e.Title,
                    Summary = e.Summary,
                    StartsAt = ToUtc(e.StartsAt),
                    EndsAt = ToUtc(e.EndsAt),
                    City = e.City,
                    Venue = e.Venue,
                    ImageUrl = e.ImageUrl,
                    SourceUrl = e.SourceUrl,
                    Tags = SplitTags(e.Tags)
                })
                .ToList();

            return Ok(events);
        }

        /// <summary>
        /// Публичная страница события по slug. Черновики и отклонённые события не отдаются.
        /// </summary>
        [HttpGet("public/{slug}")]
        public async Task<ActionResult<PublicEventDetailDto>> PublicDetail(string slug)
        {
            var item = await _context.Events
                .AsNoTracking()
                .Where(e => e.Slug == slug
                            && e.Status == EventStatus.Published
                            && e.IsActive)
                .Select(e => new PublicEventDetailDto
                {
                    Id = e.Id,
                    Slug = e.Slug,
                    Title = e.Title,
                    Summary = e.Summary,
                    Description = e.Description,
                    StartsAt = e.StartsAt,
                    EndsAt = e.EndsAt,
                    City = e.City,
                    Venue = e.Venue,
                    Address = e.Address,
                    ImageUrl = e.ImageUrl,
                    SourceUrl = e.SourceUrl,
                    Tags = SplitTags(e.Tags),
                    PublishedAt = e.PublishedAt
                })
                .FirstOrDefaultAsync();

            if (item is null)
            {
                return NotFound();
            }

            // SQL Server возвращает datetime2 без Kind, поэтому явно помечаем UTC,
            // чтобы клиент не сдвигал время в локальную зону.
            item.StartsAt = ToUtc(item.StartsAt);
            item.EndsAt = ToUtc(item.EndsAt);
            item.PublishedAt = ToUtc(item.PublishedAt);

            return Ok(item);
        }

        /// <summary>
        /// Динамический sitemap для опубликованных событий.
        /// </summary>
        [HttpGet("sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            var baseUrl = (_configuration["Events:SiteBaseUrl"] ?? "https://made4love.ru").TrimEnd('/');
            var now = DateTime.UtcNow;

            var events = await _context.Events
                .AsNoTracking()
                .Where(e => e.Status == EventStatus.Published
                            && e.IsActive
                            && (e.EndsAt ?? e.StartsAt) >= now.AddDays(-30))
                .OrderBy(e => e.StartsAt)
                .Select(e => new { e.Slug, e.UpdatedAt })
                .ToListAsync();

            var builder = new StringBuilder();
            builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            builder.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
            builder.AppendLine($"  <url><loc>{EscapeXml(baseUrl + "/events")}</loc><changefreq>daily</changefreq><priority>0.8</priority></url>");

            foreach (var item in events)
            {
                builder.AppendLine(
                    $"  <url><loc>{EscapeXml($"{baseUrl}/events/{item.Slug}")}</loc>" +
                    $"<lastmod>{item.UpdatedAt:yyyy-MM-dd}</lastmod>" +
                    "<changefreq>weekly</changefreq><priority>0.6</priority></url>");
            }

            builder.AppendLine("</urlset>");
            return Content(builder.ToString(), "application/xml", Encoding.UTF8);
        }

        /// <summary>
        /// Старый внутренний список для авторизованных пользователей.
        /// </summary>
        [Authorize]
        [HttpGet("list")]
        public async Task<ActionResult> List()
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == UserId);
            if (user is null)
            {
                return NotFound("User not found");
            }

            var events = await _context.Events
                .AsNoTracking()
                .Where(e => e.Status == EventStatus.Published
                            && e.IsActive
                            && e.CreatedAt >= DateTime.UtcNow.AddMonths(-1))
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new EventListItemDto
                {
                    IsNew = user.EventsSeen == null || e.CreatedAt >= user.EventsSeen ? 1 : 0,
                    Title = e.Title,
                    Description = e.Description,
                    CreatedAt = e.CreatedAt,
                    Slug = e.Slug,
                    StartsAt = e.StartsAt
                })
                .ToListAsync();

            return Ok(events);
        }

        [Authorize]
        [HttpPost("seen")]
        public async Task<ActionResult> Seen()
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == UserId);
            if (user is null)
            {
                return NotFound("User not found");
            }

            user.EventsSeen = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(0);
        }

        private static DateTime ToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static DateTime? ToUtc(DateTime? value) => value.HasValue ? ToUtc(value.Value) : null;

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

    public class EventListItemDto
    {
        public int IsNew { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Slug { get; set; } = string.Empty;
        public DateTime StartsAt { get; set; }
    }
}
