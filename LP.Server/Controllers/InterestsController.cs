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
    public class InterestsController : RedisController
    {
        private const string INTERESTS_CACHE_KEY = "interests:all";
        private readonly ApplicationContext _context;
        
        public InterestsController(ApplicationContext context, IDistributedCache cache): base(cache)
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            try
            {
                // Используем безопасный метод получения из кеша
                var interests = await GetFromCacheSafeAsync(
                    INTERESTS_CACHE_KEY,
                    async () => await _context.Interests
                        .IgnoreQueryFilters()
                        .OrderBy(x => x.Group)
                        .ThenBy(x => x.Name)
                        .ToListAsync(),
                    TimeSpan.FromHours(24)
                );

                return Ok(interests);
            }
            catch (Exception ex)
            {
                // Fallback: получаем данные напрямую из БД
                var interests = await _context.Interests
                    .IgnoreQueryFilters()
                    .OrderBy(x => x.Group)
                    .ThenBy(x => x.Name)
                    .ToListAsync();

                return Ok(interests);
            }
        }
    }
}
