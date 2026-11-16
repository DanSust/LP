using LP.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RejectsController : BaseAuthController
    {
        /// <summary>Окно, в течение которого повторная жалоба на спам не создаёт новую запись.</summary>
        private static readonly TimeSpan SpamReportDeduplicationWindow = TimeSpan.FromHours(24);

        public RejectsController(ApplicationContext context) : base(context)
        {
        }

        public sealed class RejectRequest
        {
            public Guid UserId { get; set; }
            public string? Caption { get; set; }
            public Reason Reason { get; set; }
        }

        /// <summary>
        /// Блокировка пользователя. Повторный вызов идемпотентен.
        /// </summary>
        [Authorize]
        [HttpPost("block/{userId:guid}")]
        public Task<IActionResult> Block(Guid userId) =>
            AddReject(userId, Reason.rsBlock, null);

        /// <summary>
        /// Жалоба на спам. Повторная жалоба на того же пользователя
        /// в течение суток не создаёт дубль.
        /// </summary>
        [Authorize]
        [HttpPost("spam/{userId:guid}")]
        public Task<IActionResult> Spam(
            Guid userId,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RejectRequest? request = null) =>
            AddReject(userId, Reason.rsSpam, request?.Caption);

        /// <summary>
        /// Универсальный метод, сохранённый для обратной совместимости.
        /// </summary>
        [Authorize]
        [HttpPost("add")]
        public Task<IActionResult> Add([FromBody] RejectRequest model)
        {
            if (model == null)
            {
                return Task.FromResult<IActionResult>(BadRequest(new { error = "Пустой запрос" }));
            }

            return AddReject(model.UserId, model.Reason, model.Caption);
        }

        /// <summary>
        /// Жалобы и блокировки, сделанные текущим пользователем.
        /// </summary>
        [Authorize]
        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            var items = await _context.Rejects
                .AsNoTracking()
                .Where(r => r.Owner == UserId)
                .OrderByDescending(r => r.Time)
                .Select(r => new
                {
                    id = r.Id,
                    userId = r.UserId,
                    reason = r.Reason,
                    caption = r.Caption,
                    time = r.Time
                })
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>
        /// Снятие блокировки.
        /// </summary>
        [Authorize]
        [HttpDelete("block/{userId:guid}")]
        public async Task<IActionResult> Unblock(Guid userId)
        {
            var removed = await _context.Rejects
                .Where(r => r.Owner == UserId && r.UserId == userId && r.Reason == Reason.rsBlock)
                .ExecuteDeleteAsync();

            return Ok(new { userId, unblocked = removed > 0 });
        }

        private async Task<IActionResult> AddReject(Guid targetUserId, Reason reason, string? caption)
        {
            if (targetUserId == Guid.Empty)
            {
                return BadRequest(new { error = "Не указан пользователь" });
            }

            if (targetUserId == UserId)
            {
                return BadRequest(new { error = "Нельзя применить действие к самому себе" });
            }

            var targetExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == targetUserId);

            if (!targetExists)
            {
                return NotFound(new { error = "Пользователь не найден" });
            }

            var existing = await _context.Rejects
                .AsNoTracking()
                .Where(r => r.Owner == UserId && r.UserId == targetUserId && r.Reason == reason)
                .OrderByDescending(r => r.Time)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                var isDuplicate = reason == Reason.rsBlock
                    || existing.Time >= DateTime.UtcNow - SpamReportDeduplicationWindow;

                if (isDuplicate)
                {
                    return Ok(new
                    {
                        id = existing.Id,
                        userId = existing.UserId,
                        reason = existing.Reason,
                        caption = existing.Caption,
                        time = existing.Time,
                        created = false
                    });
                }
            }

            var reject = new Reject
            {
                Owner = UserId,
                UserId = targetUserId,
                Reason = reason,
                Caption = caption?.Trim() ?? string.Empty,
                Time = DateTime.UtcNow
            };

            _context.Rejects.Add(reject);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = reject.Id,
                userId = reject.UserId,
                reason = reject.Reason,
                caption = reject.Caption,
                time = reject.Time,
                created = true
            });
        }
    }
}
