using Humanizer;
using LP.Common;
using LP.Entity;
using LP.Entity.Interfaces;
using LP.Entity.Store;
using LP.Server.DTO;
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
    [Route("auth")]
    public class AuthController : ControllerBase
	{
		private readonly IAuthService _service;
		private readonly IUserStore _store;
        private readonly ApplicationContext _context;
        private readonly IEmailService _emailService;
        private readonly IMemoryCache _cache;
        private const int MaxLoginAttempts = 5;
        private static readonly TimeSpan LoginLockoutDuration = TimeSpan.FromMinutes(15);

        [HttpGet("me")]
		public IActionResult Index()
		{
			return Ok();
		}
        private readonly IConfiguration _config;

		public AuthController(
            IAuthService service,
            IUserStore store,
            ApplicationContext context,
            IEmailService emailService,
            IConfiguration config,
            IMemoryCache cache
            )
		{
			_service = service;
            _emailService = emailService; 
			_store = store;
            _context = context;
            _config = config;
            _cache = cache;
        }

		[AllowAnonymous]
		[HttpGet("NoRights")]
		public IActionResult NoRights()
		{
			return NoContent();
		}

        [AllowAnonymous]
        [HttpGet("ping")]
        public IActionResult Ping() => Ok(new { time = DateTime.Now });

        [AllowAnonymous]
		[HttpGet("status")]
		public async Task<ActionResult<AuthStatus>> Status()
        {
            //Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] /status STARTED");
            //Console.WriteLine($"  Cookies: {string.Join(", ", Request.Cookies.Keys)}");
            //Console.WriteLine($"  Headers Auth: {Request.Headers.Authorization}");

            var ID = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
            string userId = "";
			foreach (var claim in User.Claims)
			{
                //Console.WriteLine($"  Claim: {claim.Type} = {claim.Value}");
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

            //Console.WriteLine($"  IsAuthenticated: {User.Identity?.IsAuthenticated}");
            //Console.WriteLine($"  UserId from claims: {userId}");

            var result = new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                hasCookie = Request.Cookies.ContainsKey("auth"),
                userId,
                claimsCount = User.Claims.Count(),
                cookiesCount = Request.Cookies.Count,
                time = DateTime.Now.ToString("HH:mm:ss.fff")
            };

            //Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] /status COMPLETED: {System.Text.Json.JsonSerializer.Serialize(result)}");

            return Ok(result);

			// токен можно передать либо в заголовке Authorization,
			// либо в query (?token=...)
			//var raw =
			//	Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "")
			//		  ?? Request.Query["token"].FirstOrDefault();

			//var (ok, email, name) = await _providers.FirstOrDefault().ValidateAsync(raw);
			//if (!ok) return Unauthorized(new AuthStatus(false, null, null));

			//return Ok(new AuthStatus(true, email, name));
		}

        private sealed record LoginAttemptInfo(int Count, DateTimeOffset? LockoutUntil);

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginModel model)
        {
            var email = model.Username?.Trim().ToLowerInvariant() ?? string.Empty;
            var cacheKey = $"login-attempts:{email}";

            if (_cache.TryGetValue<LoginAttemptInfo>(cacheKey, out var attempt) && attempt?.LockoutUntil > DateTimeOffset.UtcNow)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Слишком много попыток входа. Попробуйте позже." });
            }

            var response = await _service.Authenticate(model.Username, model.Password);
            if (response == null)
            {
                var current = _cache.TryGetValue<LoginAttemptInfo>(cacheKey, out var existing) ? existing : null;
                var count = (current?.Count ?? 0) + 1;
                DateTimeOffset? lockoutUntil = count >= MaxLoginAttempts ? DateTimeOffset.UtcNow.Add(LoginLockoutDuration) : null;
                _cache.Set(cacheKey, new LoginAttemptInfo(count, lockoutUntil), lockoutUntil ?? DateTimeOffset.UtcNow.Add(LoginLockoutDuration));

                var userExists = await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email);
                var message = lockoutUntil.HasValue
                    ? "Слишком много попыток входа. Попробуйте через 15 минут."
                    : (userExists ? "Неверный пароль" : "Пользователь с таким email не найден");
                if (lockoutUntil.HasValue)
                    return StatusCode(StatusCodes.Status429TooManyRequests, new { message });
                return Unauthorized(new { message });
            }

            _cache.Remove(cacheKey);

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
                var confirmationLink = $"/confirm?token={confirmation.ConfirmationToken}&email={user.Email}";
                try
                {
                    await _emailService.SendConfirmationEmailAsync(user.Email, confirmationLink);
                }
                catch (Exception ex)
                {
                }

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
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordModel model)
        {
            if (!ModelState.IsValid)
                return Ok(new { success = true, message = "Если аккаунт с таким email существует, мы отправили ссылку для восстановления пароля." });

            var email = model.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);

            if (user != null)
            {
                var token = Guid.NewGuid().ToString("N");
                var reset = await _context.PasswordResetTokens.FirstOrDefaultAsync(x => x.UserId == user.Id);
                if (reset == null)
                {
                    reset = new PasswordResetToken { UserId = user.Id };
                    _context.PasswordResetTokens.Add(reset);
                }

                reset.Token = token;
                reset.TokenExpires = DateTime.UtcNow.AddHours(1);
                reset.CreatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var siteBaseUrl = _config["Events:SiteBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                var resetLink = $"{siteBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email ?? email)}";
                try
                {
                    await _emailService.SendPasswordResetEmailAsync(user.Email ?? email, resetLink);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Password reset email error: {ex.Message}");
                }
            }

            return Ok(new { success = true, message = "Если аккаунт с таким email существует, мы отправили ссылку для восстановления пароля." });
        }

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Проверьте корректность данных." });

            var email = model.Email.Trim().ToLowerInvariant();
            var user = await _context.Users
                .Include(u => u.PasswordResetToken)
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email);

            if (user?.PasswordResetToken == null || user.PasswordResetToken.Token != model.Token)
                return BadRequest(new { success = false, message = "Ссылка недействительна или устарела." });

            if (user.PasswordResetToken.TokenExpires < DateTime.UtcNow)
                return BadRequest(new { success = false, message = "Срок действия ссылки истёк. Запросите восстановление снова." });

            user.PasswordHash = _service.HashPassword(model.NewPassword);
            user.PasswordResetToken.Token = null;
            user.PasswordResetToken.TokenExpires = null;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Пароль обновлён. Теперь вы можете войти." });
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
