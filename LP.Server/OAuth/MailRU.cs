using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace LP.Server.OAuth
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMailRuAuthorization(this WebApplicationBuilder builder)
        //public static IServiceCollection AddMailRuAuthorization(this IServiceCollection services, WebApplicationBuilder builder)
        {
            builder.Services.AddAuthentication().AddOAuth("MailRu", options =>
            {
                options.ClientId = builder.Configuration["MailRu:ClientId"];
                options.ClientSecret = builder.Configuration["MailRu:ClientSecret"];
                options.CallbackPath = new PathString("/signin-mailru");

                options.AuthorizationEndpoint = "https://oauth.mail.ru/login";
                options.TokenEndpoint = "https://oauth.mail.ru/token";
                options.UserInformationEndpoint = "https://oauth.mail.ru/userinfo";

                options.Scope.Add("userinfo");

                options.SaveTokens = true;

                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "email");
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                options.ClaimActions.MapJsonKey(ClaimTypes.GivenName, "first_name");
                options.ClaimActions.MapJsonKey(ClaimTypes.Surname, "last_name");
                options.ClaimActions.MapJsonKey("urn:mailru:image", "image");

                options.Events = new OAuthEvents
                {
                    OnCreatingTicket = async context =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                        var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                        response.EnsureSuccessStatusCode();

                        var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        context.RunClaimActions(user.RootElement);
                    }
                };
            });
            return builder.Services;
        }
    }
}
