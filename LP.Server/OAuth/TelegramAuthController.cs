using Azure.Core;
using LP.Common;
using LP.Entity.Interfaces;
using LP.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LP.Server.Controllers.OAuth;

public record TelegramAuthData(
    long Id,
    string FirstName,
    string? LastName,
    string? Username,
    string? PhotoUrl,
    long AuthDate,
    string Hash
);

[ApiController]
[Route("api/auth/telegram")]
[AllowAnonymous]
public class TelegramAuthController : BaseOAuthController
{
    private readonly IConfiguration _config;

    public TelegramAuthController(
        IUserStore userStore,
        IAuthService authService,
        IConfiguration config,
        ILogger<TelegramAuthController> logger)
        : base(userStore, authService, logger)
    {
        _config = config;
    }

    /// <summary>
    /// Получить параметры для Telegram Login Widget
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(new
        {
            botName = _config["OAuth:Telegram:BotName"],
            authUrl = "/api/oauth/telegram/verify"
        });
    }

    /// <summary>
    /// Проверка данных от Telegram Login Widget
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] TelegramAuthData data)
    {
        if (!VerifySignature(data))
            return Unauthorized(new { message = "Invalid signature" });

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now - data.AuthDate > 86400)
            return Unauthorized(new { message = "Auth data expired" });

        var userClaims = new UserClaims(
            ProviderId: data.Id.ToString(),
            Email: null, // Telegram не дает email
            Username: data.Username,
            FirstName: data.FirstName,
            LastName: data.LastName,
            FullName: $"{data.FirstName} {data.LastName}".Trim(),
            AvatarUrl: data.PhotoUrl
        );

        var user = await UserStore.GetOrCreateAsync(userClaims, "telegram");
        await SignInAsync(user);

        var token = AuthService.GenerateToken(user);

        // 7. Создаем claims identity
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, user.Username ?? ""),
            new Claim("provider", "telegram"),
            new Claim("access_token", token.Token)
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

        return Ok(new
        {
            success = true,
            userId = user.Id,
            username = user.Username,
            token = token.Token
        });
    }

    private bool VerifySignature(TelegramAuthData data)
    {
        var botToken = _config["OAuth:Telegram:BotToken"];

        // Создаем список полей в правильном порядке
        var checkList = new List<string>
        {
            $"auth_date={data.AuthDate}",
            $"first_name={data.FirstName}",
            $"id={data.Id}"
        };

        if (!string.IsNullOrEmpty(data.LastName))
            checkList.Add($"last_name={data.LastName}");

        if (!string.IsNullOrEmpty(data.Username))
            checkList.Add($"username={data.Username}");

        if (!string.IsNullOrEmpty(data.PhotoUrl))
            checkList.Add($"photo_url={data.PhotoUrl}");

        // Сортируем для надежности
        checkList.Sort();

        var checkString = string.Join("\n", checkList);

        // Вычисляем hash
        using var sha256 = SHA256.Create();
        var secret = sha256.ComputeHash(Encoding.UTF8.GetBytes(botToken));

        using var hmac = new HMACSHA256(secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(checkString));
        var computedHash = BitConverter.ToString(hash).Replace("-", "").ToLower();

        return computedHash == data.Hash.ToLower();
    }
}