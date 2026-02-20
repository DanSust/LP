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
    public class RejectsController : BaseAuthController
    {
        private readonly ApplicationContext _context;

        public RejectsController(ApplicationContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpPost("add")]
        public async Task<ActionResult> Add([FromBody] Reject model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == UserId);

            var item = _context.Rejects.Add(new Reject()
            { Caption = model.Caption, Reason = model.Reason, Owner = user.Id, UserId = model.UserId });
            await _context.SaveChangesAsync();

            return Ok(item);
        }
    }
}