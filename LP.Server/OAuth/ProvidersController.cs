using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LP.Server.OAuth;

[ApiController]
[Route("api/oauth")]
[AllowAnonymous]
public class ProvidersController : ControllerBase
{
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        return Ok(new object[]
        {
            new 
            {
                name = "Google",
                type = "oauth2",
                loginUrl = "/api/oauth/google/login",
                icon = "google"
            },
            new
            {
                name = "VK",
                type = "oauth2",
                loginUrl = "/api/oauth/vk/login",
                icon = "vk"
            },
            new
            {
                name = "Telegram",
                type = "widget",
                configUrl = "/api/oauth/telegram/config",
                icon = "telegram",
                description = "Authorization via Telegram Login Widget"
            }
        });
    }
}