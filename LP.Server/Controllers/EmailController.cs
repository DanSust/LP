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
    public class EmailController : BaseAuthController
    {
        private readonly ApplicationContext _context;
        private readonly IEmailService _service;

        public EmailController(ApplicationContext context, IEmailService service)
        {
            _context = context;
            _service = service;
        }

        [Authorize]
        [HttpPost("auth")]
        public async Task<IActionResult> Auth([FromQuery] string email, [FromQuery] string link)
        {
            // 1. Генерируем уникальный токен
            var token = Guid.NewGuid().ToString();

            // 2. Сохраняем токен в базу для этого пользователя (LP.Entity.EmailConfirmation)
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return NotFound();

            user.EmailConfirmation = new EmailConfirmation
            {
                ConfirmationToken = token,
                TokenExpires = DateTime.UtcNow.AddHours(24),
                IsConfirmed = false
            };
            await _context.SaveChangesAsync();

            // 3. Формируем полную ссылку с параметрами
            var confirmationLink = $"https://made4love.ru/api/Email/confirm?email={email}&token={token}";

            // 4. Отправляем письмо
            await _service.SendConfirmationEmailAsync(email, confirmationLink);

            return Ok(new { message = "Ссылка сформирована и отправлена" });
        }

        [AllowAnonymous]
        [HttpGet("confirm")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string token, [FromQuery] string email)
        {
            var user = await _context.Users
                .Include(u => u.EmailConfirmation)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user?.EmailConfirmation == null)
                return BadRequest(new { message = "Пользователь не найден" });

            if (user.EmailConfirmation.ConfirmationToken != token)
                return BadRequest(new { message = "Неверный токен" });

            if (user.EmailConfirmation.TokenExpires < DateTime.UtcNow)
                return BadRequest(new { message = "Срок действия токена истек" });

            // Подтверждаем
            user.EmailConfirmation.IsConfirmed = true;
            user.EmailConfirmation.ConfirmationToken = null;
            user.EmailConfirmation.TokenExpires = null;

            await _context.SaveChangesAsync();

            //return Ok(new { message = "Email успешно подтвержден" });
            return LocalRedirect("/profile");
        }
    }
}