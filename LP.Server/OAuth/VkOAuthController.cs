// LP.Server/Controllers/OAuth/VkOAuthController.cs
using AspNet.Security.OAuth.Vkontakte;
using LP.Entity;
using LP.Entity.Interfaces;
using LP.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LP.Common;

namespace LP.Server.Controllers.OAuth;

[ApiController]
[Route("api/oauth/vk")]
[AllowAnonymous]
public class VkOAuthController : BaseOAuthController
{
    public VkOAuthController(
        IUserStore userStore,
        IAuthService authService,
        ILogger<VkOAuthController> logger)
        : base(userStore, authService, logger)
    {
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
}