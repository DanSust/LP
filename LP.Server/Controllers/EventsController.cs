using LP.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EventsController : BaseAuthController
    {
        private readonly ApplicationContext _context;

        public EventsController(ApplicationContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet("list")]
        public async Task<ActionResult> List()
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == UserId);

            var list = await _context.Events.Where(x => x.CreatedAt >= DateTime.UtcNow.AddMonths(-1))
                .Select(item => new { isNew = item.CreatedAt >= user.EventsSeen? 1 : 0 , item.Title, item.Description, item.CreatedAt})
                .OrderByDescending(x=>x.CreatedAt)
                .ToListAsync();

            return Ok(list);
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
}
