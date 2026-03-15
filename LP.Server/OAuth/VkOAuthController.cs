// LP.Server/Controllers/OAuth/VkOAuthController.cs
using AspNet.Security.OAuth.Vkontakte;
using Azure.Core;
using LP.Common;
using LP.Entity;
using LP.Entity.Interfaces;
using LP.Entity.Store;
using LP.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LP.Server.Controllers.OAuth;

public class VkTokenRequest
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }

    [JsonPropertyName("user_id")]
    public long UserId { get; set; } // В JSON это число 604229
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
    public string? State { get; set; } // Если имя совпадает, атрибут не обязателен
}

[ApiController]
[Route("oauth/vk")]
[AllowAnonymous]
public class VkOAuthController : BaseOAuthController
{
    private readonly IHttpClientFactory _httpClientFactory;
    public VkOAuthController(
        IUserStore userStore,
        IAuthService authService,
        ILogger<VkOAuthController> logger,
        IHttpClientFactory httpClientFactory
        )
        : base(userStore, authService, logger)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = Url.Action("Callback", new { returnUrl })
        }, VkontakteAuthenticationDefaults.AuthenticationScheme);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string? returnUrl = null)
    {
        var result = await HttpContext.AuthenticateAsync(VkontakteAuthenticationDefaults.AuthenticationScheme);

        if (!result.Succeeded)
        {
            Logger.LogWarning("VK auth failed: {Error}", result.Failure?.Message);
            return AuthError("vk", result.Failure?.Message);
        }

        var user = await ProcessUserAsync(result.Principal);
        await SignInAsync(user);

        return CallbackResponse(user, "vk", returnUrl);
    }

    private async Task<User> ProcessUserAsync(ClaimsPrincipal? principal)
    {
        if (principal == null)
            throw new InvalidOperationException("Principal is null");

        var claims = principal.Claims.ToDictionary(c => c.Type, c => c.Value);

        var firstName = claims.GetValueOrDefault(ClaimTypes.GivenName);
        var lastName = claims.GetValueOrDefault(ClaimTypes.Surname);

        var userClaims = new UserClaims(
            ProviderId: claims.GetValueOrDefault(ClaimTypes.NameIdentifier)
                        ?? throw new InvalidOperationException("No ID from VK"),
            Email: claims.GetValueOrDefault(ClaimTypes.Email),
            Username: claims.GetValueOrDefault("urn:vkontakte:screen_name"),
            FirstName: firstName,
            LastName: lastName,
            FullName: $"{firstName} {lastName}".Trim(),
            AvatarUrl: claims.GetValueOrDefault("urn:vkontakte:photo:max_orig")
        );

        return await UserStore.GetOrCreateAsync(userClaims, "vk");
    }

    // Controllers/OAuth/VkOAuthController.cs

    [HttpPost("verify-token")]
    public async Task<IActionResult> VerifyToken([FromBody] VkTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.AccessToken))
            return BadRequest("Access token is missing");

        try
        {
            // 1. Проверяем токен через API VK (users.get)
            // В реальном проекте лучше вынести это в отдельный VkService
            //using var client = new HttpClient();
            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; AcmeInc/1.0)");

            var vkUrl = $"https://api.vk.com/method/users.get?access_token={request.AccessToken}&v=5.131&fields=photo_max_orig,screen_name";
            var response = await client.GetFromJsonAsync<VkUserResponse>(vkUrl);
            var rawResponse = await client.GetStringAsync(vkUrl);
            Console.WriteLine($"VK API Raw Response: {rawResponse}");

            var vkUser = response?.Response?.FirstOrDefault();
            if (vkUser == null)
                return Unauthorized("Invalid VK token");

            // 2. Формируем UserClaims для твоего UserStore
            var userClaims = new UserClaims(
                ProviderId: vkUser.Id.ToString(),
                Email: null, // VK ID SDK не всегда отдает email без специальных разрешений
                Username: vkUser.ScreenName,
                FirstName: vkUser.FirstName,
                LastName: vkUser.LastName,
                FullName: $"{vkUser.FirstName} {vkUser.LastName}".Trim(),
                AvatarUrl: vkUser.PhotoMaxOrig
            );

            // 3. Получаем или создаем пользователя в БД
            var user = await UserStore.GetOrCreateAsync(userClaims, "vk");

            // 4. Генерируем JWT токен твоей системы (как при обычном логине)
            var accessToken = AuthService.GenerateToken(user); // Предполагаю, у тебя есть такой метод

            // 7. Создаем claims identity
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, user.Username ?? ""),
            new Claim("provider", "vk"),
            new Claim("access_token", accessToken.Token)
        };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // 8. Подписываем cookie
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
                });

            Console.WriteLine($"User {user.Id} signed in successfully");

            // 9. Редирект
            Response.Cookies.Append("UserId",
                user.Id.ToString(),
                new CookieOptions
                {
                    HttpOnly = false,      // Prevents JavaScript access (XSS protection)
                    Secure = true,        // Only sent over HTTPS
                    SameSite = SameSiteMode.Lax, // CSRF protection
                    Path = "/", // Доступно для всех путей
                    Domain = null, // Автоматически текущий домен
                    Expires = DateTimeOffset.UtcNow.AddMinutes(20) // Expiration
                });

            return Ok(new
            {
                user_id = user.Id,
                Success = true,
                Token = accessToken.Token,
                User = user
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error verifying VK token");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("verify-token-fast")]
    public async Task<IActionResult> VerifyTokenFast([FromBody] VkTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.IdToken))
            return BadRequest("IdToken is missing");

        try
        {
            // 1. Декодируем ID (sub) из токена вручную (как ты уже делал)
            var parts = request.IdToken.Split('.');
            if (parts.Length < 2) return BadRequest("Invalid JWT format");

            var payloadBase64 = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payloadBase64.Length % 4)
            {
                case 2: payloadBase64 += "=="; break;
                case 3: payloadBase64 += "="; break;
            }

            var bytes = Convert.FromBase64String(payloadBase64);
            var json = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Извлекаем 'sub' (это наш надежный идентификатор)
            if (!root.TryGetProperty("sub", out var subProp))
                return Unauthorized("User ID (sub) not found in token");

            var vkUserId = subProp.ValueKind == JsonValueKind.Number
                ? subProp.GetInt64().ToString()
                : subProp.GetString();

            // 2. ИСПОЛЬЗУЕМ ДАННЫЕ ИЗ REQUEST (которые мы увидели в QuickWatch)
            // Если данных в request нет, используем значения из токена или дефолты
            string firstName = !string.IsNullOrEmpty(request.FirstName)
                ? request.FirstName
                : (root.TryGetProperty("given_name", out var fn) ? fn.GetString() ?? "VK" : "VK");

            string lastName = !string.IsNullOrEmpty(request.LastName)
                ? request.LastName
                : (root.TryGetProperty("family_name", out var ln) ? ln.GetString() ?? "" : "");

            string? avatarUrl = !string.IsNullOrEmpty(request.AvatarUrl)
                ? request.AvatarUrl
                : (root.TryGetProperty("picture", out var pic) ? pic.GetString() : null);

            // Email в VK ID обычно приходит отдельным полем в request или в userInfo
            string? email = request.Email;
            if (string.IsNullOrEmpty(email) && root.TryGetProperty("email", out var em))
                email = em.GetString();

            var userClaims = new UserClaims(
                ProviderId: vkUserId!,
                Email: email,
                Username: $"id{vkUserId}",
                FirstName: firstName,
                LastName: lastName,
                FullName: $"{firstName} {lastName}".Trim(),
                AvatarUrl: avatarUrl
            );

            // 3. Сохраняем и авторизуем
            var user = await UserStore.GetOrCreateAsync(userClaims, "vk");
            var systemToken = AuthService.GenerateToken(user);

            await SignInAsync(user);

            return Ok(new
            {
                user_id = user.Id,
                Success = true,
                Token = systemToken.Token,
                User = user
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Manual VK JWT Parsing Error");
            return StatusCode(500, "Ошибка разбора: " + ex.Message);
        }
    }

    // Вспомогательные классы для парсинга ответа VK
    public class VkUserResponse { public List<VkUserInfo> Response { get; set; } }
    public class VkUserInfo
    {
        public long Id { get; set; }
        [JsonPropertyName("first_name")] public string FirstName { get; set; }
        [JsonPropertyName("last_name")] public string LastName { get; set; }
        [JsonPropertyName("photo_max_orig")] public string PhotoMaxOrig { get; set; }
        [JsonPropertyName("screen_name")] public string ScreenName { get; set; }
    }
}