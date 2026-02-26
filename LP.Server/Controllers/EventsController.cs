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
    public class EventsController : RedisController
    {
        private const string EVENTS_CACHE_KEY = "events:list";
        private readonly ApplicationContext _context;
        
        public EventsController(ApplicationContext context, IDistributedCache cache) : base(cache)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet("list")]
        public async Task<ActionResult> List()
        {
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == UserId);
                if (user == null)
                {
                    return NotFound("User not found");
                }

                // Используем безопасный метод получения из кеша
                var events = await GetFromCacheSafeAsync(
                    EVENTS_CACHE_KEY,
                    async () =>
                    {
                        return await _context.Events
                            .Where(x => x.CreatedAt >= DateTime.UtcNow.AddMonths(-1))
                            .Select(item => new EventListItemDto
                            {
                                IsNew = item.CreatedAt >= user.EventsSeen ? 1 : 0,
                                Title = item.Title,
                                Description = item.Description,
                                CreatedAt = item.CreatedAt
                            })
                            .OrderByDescending(x => x.CreatedAt)
                            .ToListAsync();
                    },
                    TimeSpan.FromDays(1)
                );

                return Ok(events);
            }
            catch (Exception ex)
            {
                // Fallback: получаем данные напрямую из БД
                var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == UserId);
                if (user == null)
                {
                    return NotFound("User not found");
                }

                var events = await _context.Events
                    .Where(x => x.CreatedAt >= DateTime.UtcNow.AddMonths(-1))
                    .Select(item => new EventListItemDto
                    {
                        IsNew = item.CreatedAt >= user.EventsSeen ? 1 : 0,
                        Title = item.Title,
                        Description = item.Description,
                        CreatedAt = item.CreatedAt
                    })
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();

                return Ok(events);
            }
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
