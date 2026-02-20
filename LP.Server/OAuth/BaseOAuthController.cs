using LP.Entity;
using LP.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LP.Entity.Interfaces;

namespace LP.Server.Controllers.OAuth;

public abstract class BaseOAuthController : ControllerBase
{
    protected readonly IUserStore UserStore;
    protected readonly IAuthService AuthService;
    protected readonly ILogger Logger;

    protected BaseOAuthController(
        IUserStore userStore,
        IAuthService authService,
        ILogger logger)
    {
        UserStore = userStore;
        AuthService = authService;
        Logger = logger;
    }

    protected async Task SignInAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("Provider", user.Provider ?? "unknown")
        };

        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(ClaimTypes.Email, user.Email));

        if (!string.IsNullOrEmpty(user.Username))
            claims.Add(new Claim(ClaimTypes.Name, user.Username));

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

        Response.Cookies.Append("UserId", user.Id.ToString(), new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    protected IActionResult CallbackResponse(User user, string provider, string? returnUrl)
    {
        var targetUrl = returnUrl ?? $"https://127.0.0.1/auth/success?provider={provider}&userId={user.Id}";

        var html = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'/></head>
<body>
    <script>
        (function() {{
            var data = {{
                type: 'OAUTH_SUCCESS',
                provider: '{provider}',
                userId: '{user.Id}',
                username: '{user.Username?.Replace("'", "\\'")}',
                email: '{user.Email?.Replace("'", "\\'")}'
            }};
            if (window.opener) {{
                window.opener.postMessage(data, '*');
                setTimeout(function() {{ window.close(); }}, 500);
            }} else {{
                window.location.href = '{targetUrl.Replace("'", "\\'")}';
            }}
        }})();
    </script>
</body>
</html>";

        return Content(html, "text/html");
    }

    protected IActionResult AuthError(string provider, string? error)
    {
        var message = Uri.EscapeDataString(error ?? "unknown");
        return Redirect($"/auth/error?provider={provider}&error={message}");
    }
}