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
    public class QuestionsController : BaseAuthController
    {
        private readonly ApplicationContext _context;
        
        public QuestionsController(ApplicationContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var list = await _context.UserQuestions
                .Where(x=>x.User.Id == UserId)
                .OrderBy(x => x.Order)
                .ToListAsync();

            return Ok(list);
        }
    }
}
