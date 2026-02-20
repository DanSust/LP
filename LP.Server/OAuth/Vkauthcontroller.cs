using LP.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace LP.Server.OAuth;

[ApiController]
[Route("api/auth/vk")]
public class VkAuthController : ControllerBase
{
    private readonly VkProvider _vkProvider;
    private readonly IAuthService _authService;
    private readonly ILogger<VkAuthController> _logger;
    private readonly IMemoryCache _cache;

    public VkAuthController(
        VkProvider vkProvider,
        IAuthService authService,
        ILogger<VkAuthController> logger,
        IMemoryCache cache)
    {
        _vkProvider = vkProvider;
        _authService = authService;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Инициирует OAuth процесс для VK ID
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login()
    {
        // Генерируем state для защиты от CSRF
        var state = Guid.NewGuid().ToString("N");

        // Сохраняем state в кеше на 10 минут
        var cacheKey = $"vk_oauth_state_{state}";
        _cache.Set(cacheKey, state, TimeSpan.FromMinutes(10));

        // Перенаправляем на VK
        var authUrl = _vkProvider.BuildAuthUrl(state);
        return Redirect(authUrl);
    }

    /// <summary>
    /// Callback endpoint для VK OAuth
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error)
    {
        // Проверяем наличие ошибки
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("VK OAuth error: {Error}", error);
            return Redirect($"/auth?error={error}");
        }

        // Проверяем code
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest("Authorization code is missing");
        }

        // Проверяем state для защиты от CSRF
        var savedState = _cache.Get("vk_oauth_state").ToString();
        if (string.IsNullOrEmpty(savedState) || savedState != state)
        {
            _logger.LogWarning("VK OAuth state mismatch");
            return BadRequest("Invalid state parameter");
        }

        try
        {
            // Обмениваем code на токены
            var tokenResponse = await _vkProvider.ExchangeCodeAsync(code);

            // Получаем информацию о пользователе
            var user = await _vkProvider.GetUserInfoAsync(tokenResponse.AccessToken);

            // Проверяем, существует ли пользователь в нашей БД
            // Если нет - создаём нового пользователя
            var authResponse = _authService.GenerateToken(user);

            // Очищаем state из сессии
            _cache.Remove("vk_oauth_state");

            // Перенаправляем на фронтенд с токеном
            return Redirect($"/auth/success?token={authResponse.Token}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during VK OAuth callback");
            return Redirect("/auth?error=oauth_failed");
        }
    }

    /// <summary>
    /// Валидация VK ID токена
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateToken([FromBody] TokenValidationRequest request)
    {
        if (string.IsNullOrEmpty(request.IdToken))
        {
            return BadRequest("Token is required");
        }

        var (isValid, email, name) = await _vkProvider.ValidateAsync(request.IdToken);

        if (!isValid)
        {
            return Unauthorized("Invalid token");
        }

        return Ok(new
        {
            valid = true,
            email,
            name
        });
    }
}

public record TokenValidationRequest(string IdToken);