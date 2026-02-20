using AspNet.Security.OAuth.Vkontakte;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace LP.Server.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddAppAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "auth"; // имя куки
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Domain = null;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                options.AccessDeniedPath = "/NoRights";
                options.LoginPath = "/auth";
            })
            .AddGoogle(options =>
            {
                options.ClientId = config["OAuth:Google:ClientId"]!;
                options.ClientSecret = config["OAuth:Google:ClientSecret"]!;
                options.Scope.Add("openid");
                options.Scope.Add("email");
                options.Scope.Add("profile");
                options.SaveTokens = true;
                options.CorrelationCookie = new CookieBuilder
                {
                    Name = ".AspNetCore.Correlation.Google",
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    SecurePolicy = CookieSecurePolicy.SameAsRequest,
                    MaxAge = TimeSpan.FromMinutes(10),
                    IsEssential = true
                };

                options.Events = new OAuthEvents
                {
                    OnRedirectToAuthorizationEndpoint = context =>
                    {
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    }
                };
            })
            .AddVkontakte(options =>
            {
                options.ClientId = config["OAuth:VK:ClientId"]!;
                options.ClientSecret = config["OAuth:VK:ClientSecret"]!;
                options.CallbackPath = "/api/oauth/vk/callback";
                options.Scope.Add("email");
                options.Scope.Add("phone");
                options.Fields.Add("photo_max_orig");
                options.Fields.Add("screen_name");
                options.SaveTokens = true;
            });

        services.AddAuthorization();

        return services;
    }
}