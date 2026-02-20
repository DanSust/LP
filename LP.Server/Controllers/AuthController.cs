using Humanizer;
using LP.Common;
using LP.Entity;
using LP.Entity.Interfaces;
using LP.Entity.Store;
using LP.Server.OAuth;
using LP.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Web;

namespace LP.Server.Controllers
{
	public record AuthStatus(bool IsAuthenticated, string? Email, string? Name);
	
	[ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
	{
		private readonly IAuthService _service;
		private readonly IUserStore _store;
        private readonly ApplicationContext _context;
        private readonly IEmailService _emailService;

        [HttpGet("me")]
		public IActionResult Index()
		{
			return Ok();
		}
		public AuthController(
            IAuthService service,
            IUserStore store,
            ApplicationContext context,
            IEmailService emailService
            )
		{
			_service = service;
            _emailService = emailService; 
			_store = store;
            _context = context;
        }

		[AllowAnonymous]
		[HttpGet("NoRights")]
		public IActionResult NoRights()
		{
			return NoContent();
		}

		[AllowAnonymous]
		[HttpGet("status")]
		public async Task<ActionResult<AuthStatus>> Status()
        {
            var ID = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
            string userId = "";
			foreach (var claim in User.Claims)
			{
				if (claim.Type == ClaimTypes.NameIdentifier)
                    userId = claim.Value;
			}

            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == Guid.Parse(userId));
                if (user != null)
                {
                    user.LastLogin = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new
			{
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
				hasCookie = Request.Cookies.ContainsKey("auth"),
                userId,
                claimsCount = User.Claims.Count()
			});

			// токен можно передать либо в заголовке Authorization,
			// либо в query (?token=...)
			//var raw =
			//	Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "")
			//		  ?? Request.Query["token"].FirstOrDefault();

			//var (ok, email, name) = await _providers.FirstOrDefault().ValidateAsync(raw);
			//if (!ok) return Unauthorized(new AuthStatus(false, null, null));

			//return Ok(new AuthStatus(true, email, name));
		}

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginModel model)
        {
            var response = await _service.Authenticate(model.Username, model.Password);
            if (response == null)
                return Unauthorized(new { message = "Invalid credentials" });

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, response.UserId.ToString()),
                new Claim(ClaimTypes.Email, response.Email),
                new Claim("Caption", "")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                });

            Response.Cookies.Append("UserId",
                response.UserId.ToString(),
                new CookieOptions
                {
                    HttpOnly = false,      // Prevents JavaScript access (XSS protection)
                    Secure = true,        // Only sent over HTTPS
                    SameSite = SameSiteMode.Lax, // CSRF protection
                    Path = "/", // Доступно для всех путей
                    Domain = null, // Автоматически текущий домен
                    Expires = DateTimeOffset.UtcNow.AddMinutes(20) // Expiration
                });

            return Ok(response);
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] LoginModel model)
        {
            try
            {
                var user = await _service.RegisterAsync(model.Username, model.Password);

                var confirmation = new EmailConfirmation
                {
                    UserId = user.UserId,
                    ConfirmationToken = Guid.NewGuid().ToString("N"),
                    TokenExpires = DateTime.UtcNow.AddDays(3)
                };

                _context.EmailConfirmations.Add(confirmation);
                await _context.SaveChangesAsync();

                // Отправляем письмо
                var confirmationLink = $"https://127.0.0.1/confirm?token={confirmation.ConfirmationToken}&email={user.Email}";
                await _emailService.SendConfirmationEmailAsync(user.Email, confirmationLink);

                return Ok(new
                {
                    Success = true,
                    message = "Регистрация успешна. Проверьте почту для подтверждения.",
                    userId = user.UserId
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Success = false, message = ex.Message });
            }
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

            return Ok(new { message = "Email успешно подтвержден" });
        }

        [AllowAnonymous]
        [HttpGet("logout")]
		//[ValidateAntiForgeryToken]
		public async Task<IActionResult> Logout()
		{
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            Response.Cookies.Delete("UserId", new CookieOptions
            {
                Path = "/",
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });
            Response.Cookies.Delete("auth", new CookieOptions
            {
                Path = "/",
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });

            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            return Ok(new { success = true }); // JSON, не редирект!
        }
    }
}
