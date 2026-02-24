using LP.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Generic;
using System.Text.Json;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EventsController : BaseAuthController
    {
        private readonly ApplicationContext _context;
        private readonly IDistributedCache _cache;
        public EventsController(ApplicationContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [Authorize]
        [HttpGet("list")]
        public async Task<ActionResult> List()
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == UserId);

            // 🔥 Кешируем только список событий (общий для всех)
            var cacheKey = "events:list";
            var cached = await _cache.GetStringAsync(cacheKey);

            List<EventListItemDto> events;

            if (!string.IsNullOrEmpty(cached))
            {
                events = JsonSerializer.Deserialize<List<EventListItemDto>>(cached);
            }
            else
            {

                events = await _context.Events.Where(x => x.CreatedAt >= DateTime.UtcNow.AddMonths(-1))
                    .Select(item => new EventListItemDto
                    {
                        IsNew = item.CreatedAt >= user.EventsSeen ? 1 : 0,
                        Title = item.Title,
                        Description = item.Description,
                        CreatedAt = item.CreatedAt
                    })
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();

                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(events),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
                    }
                );
            }

            return Ok(events);
        }

        [Authorize]
        [HttpPost("seen")]
        public async Task<ActionResult> Seen()
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == UserId);
            user.EventsSeen = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(0);
        }
    }

    public class EventListItemDto
    {
        public int IsNew { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
