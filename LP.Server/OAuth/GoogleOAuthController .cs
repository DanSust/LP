// LP.Server/Controllers/OAuth/GoogleOAuthController.cs
using LP.Common;
using LP.Entity;
using LP.Entity.Interfaces;
using LP.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace LP.Server.Controllers.OAuth;

[ApiController]
[Route("api/oauth/google")]
[AllowAnonymous]
public class GoogleOAuthController : BaseOAuthController
{
    private IConfiguration _configuration;
    public GoogleOAuthController(
        IUserStore userStore,
        IAuthService authService,
        IConfiguration configuration,
        ILogger<GoogleOAuthController> logger)
        : base(userStore, authService, logger)
    {
        _configuration = configuration;
    }

    private string BuildAuthUrl(string state) =>
        $"https://accounts.google.com/o/oauth2/v2/auth?" +
        $"client_id={_configuration["OAuth:Google:ClientId"]}" +
        $"&redirect_uri={_configuration["OAuth:Google:RedirectUri"]}" +
        $"&response_type=code&scope=openid%20email%20profile" +
        $"&state={state}";

    [HttpGet("login")]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {

        // 1. Полностью очищаем аутентификацию
        //await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        //// 2. Удаляем все существующие куки
        //foreach (var cookie in Request.Cookies.Keys)
        //{
        //    Response.Cookies.Delete(cookie, new CookieOptions
        //    {
        //        Path = "/",
        //        Secure = true,
        //        HttpOnly = true,
        //        SameSite = SameSiteMode.Lax
        //    });
        //    Console.WriteLine($"Deleted cookie: {cookie}");
        //}

        // 3. Формируем callback URL - ВАЖНО: используем тот же URL, что в Google Cloud Console
        var state = Guid.NewGuid().ToString("N");

        // Сохраняем state в сессию или cookie
        Response.Cookies.Append("GoogleState", state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10)
        });

        // Сохраняем returnUrl
        if (!string.IsNullOrEmpty(returnUrl))
        {
            Response.Cookies.Append("ReturnUrl", returnUrl, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10)
            });
        }

        var googleAuthUrl = BuildAuthUrl(state);

        return Redirect(googleAuthUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        Console.WriteLine($"=== GOOGLE CALLBACK IN CONTROLLER ===");
        Console.WriteLine($"Code: {code}");
        Console.WriteLine($"State: {state}");

        // 1. Проверяем state
        if (!Request.Cookies.TryGetValue("GoogleState", out var savedState) || savedState != state)
        {
            Logger.LogWarning("Invalid state parameter");
            return AuthError("google", "Invalid state");
        }

        // 2. Удаляем state cookie
        Response.Cookies.Delete("GoogleState");

        // 3. Получаем returnUrl
        Request.Cookies.TryGetValue("ReturnUrl", out var returnUrl);
        Response.Cookies.Delete("ReturnUrl");

        try
        {
            // 4. Обмениваем code на токены
            using var httpClient = new HttpClient();

            var tokenRequest = new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _configuration["OAuth:Google:ClientId"]!,
                ["client_secret"] = _configuration["OAuth:Google:ClientSecret"]!,
                ["redirect_uri"] = _configuration["OAuth:Google:RedirectUri"],
                ["grant_type"] = "authorization_code"
            };

            var tokenResponse = await httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(tokenRequest)
            );

            tokenResponse.EnsureSuccessStatusCode();
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokens = JsonSerializer.Deserialize<Dictionary<string, object>>(tokenJson);
            var accessToken = tokens["access_token"].ToString();

            // 5. Получаем информацию о пользователе
            var userInfoResponse = await httpClient.GetAsync(
                $"https://www.googleapis.com/oauth2/v3/userinfo?access_token={accessToken}"
            );

            userInfoResponse.EnsureSuccessStatusCode();
            var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(userInfoJson);

            // 6. Создаем или получаем пользователя
            var userClaims = new UserClaims(
                ProviderId: userInfo.sub,
                Email: userInfo.email,
                Username: null,
                FirstName: userInfo.given_name,
                LastName: userInfo.family_name,
                FullName: userInfo.name,
                AvatarUrl: userInfo.picture
            );

            var user = await UserStore.GetOrCreateAsync(userClaims, "google");

            // 7. Создаем claims identity
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, user.Username ?? ""),
            new Claim("provider", "google"),
            new Claim("access_token", accessToken)
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

            string html = $@"
						   <!doctype html>
						   <html>
						   <head>
							 <meta charset='utf-8'/>							 
						   </head>
						   <body>
							 <p>Authorization successful. You can close this tab.</p>
							 
							 <script>
        // Даём время браузеру сохранить куки
        setTimeout(function() {{{{
            window.location.href = 'https://127.0.0.1/authcallback?code={code}&userId={user.Id}';
        }}}}, 500); // 100ms достаточно
    </script>
							</body>
							</html>
							";

            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Google callback error");
            return AuthError("google", ex.Message);
        }
    }

    public class GoogleUserInfo
    {
        public string sub { get; set; }
        public string name { get; set; }
        public string given_name { get; set; }
        public string family_name { get; set; }
        public string picture { get; set; }
        public string email { get; set; }
        public bool email_verified { get; set; }
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
                        ?? claims.GetValueOrDefault("sub")
                        ?? throw new InvalidOperationException("No ID from Google"),
            Email: claims.GetValueOrDefault(ClaimTypes.Email),
            Username: null, // Google не дает username
            FirstName: firstName,
            LastName: lastName,
            FullName: $"{firstName} {lastName}".Trim(),
            AvatarUrl: claims.GetValueOrDefault("picture")
        );

        // UserStore сам разбирает claims
        return await UserStore.GetOrCreateAsync(userClaims, "google");
    }
}