using System.Text.Json;
using LP.Entity;
using LP.Entity.Store;
using LP.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InterestsController : ControllerBase
    {
        private readonly ApplicationContext _context;
        private readonly IDistributedCache _cache;
        public InterestsController(ApplicationContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [AllowAnonymous]
        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var cachedList = await _cache.GetStringAsync("interests:all");
            if (cachedList != null)
            {
                return Ok(JsonSerializer.Deserialize<List<Interest>>(cachedList));
            }

            var list = await _context.Interests
                .IgnoreQueryFilters()
                .OrderBy(x => x.Group)
                .ThenBy(x => x.Name)
                .ToListAsync();

            // Сохраняем в кеш на 
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            };

            await _cache.SetStringAsync(
                "interests:all",
                JsonSerializer.Serialize(list),
                cacheOptions
            );

            return Ok(list);
        }
    }
}
