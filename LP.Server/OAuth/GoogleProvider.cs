using LP.Entity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LP.Server.OAuth;

public record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("id_token")] string IdToken
);

public sealed class GoogleProvider : IOAuthProvider
{
    private readonly IConfiguration _cfg;
    private readonly HttpClient _http;
    private IMemoryCache _cache { get; set; }

    public string Name => "Google";

    IMemoryCache IOAuthProvider._cache { get => _cache; set => _cache = value; }

    public GoogleProvider(IHttpClientFactory f, IConfiguration c, IMemoryCache m)
    {
        _cfg = c;
        _http = f.CreateClient();
        _cache = m;
    }

    public string BuildAuthUrl(string state) =>
        $"https://accounts.google.com/o/oauth2/v2/auth?" +
        $"client_id={_cfg["OAuth:Google:ClientId"]}" +
        $"&redirect_uri={_cfg["OAuth:Google:RedirectUri"]}" +
        $"&response_type=code&scope=openid%20email%20profile" +
        $"&state={state}";

    public async Task<TokenResponse> ExchangeCodeAsync(string code)
    {
        var dict = new Dictionary<string, string>
        {
            ["client_id"] = _cfg["OAuth:Google:ClientId"],
            ["client_secret"] = _cfg["OAuth:Google:ClientSecret"],
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = _cfg["OAuth:Google:RedirectUri"]
        };
        using var res = await _http.PostAsync(
            "https://oauth2.googleapis.com/token",   // без пробела
            new FormUrlEncodedContent(dict));
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<TokenResponse>();
    }

    public async Task<User> GetUserInfoAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            "https://www.googleapis.com/oauth2/v1/userinfo?alt=json");
        req.Headers.Authorization = new("Bearer", accessToken);
        using var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return new User();
    }

    public async Task<(bool ok, string? email, string? name)> ValidateAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, null, null);

        // 1. Download Google public keys
        var jwks = await _http.GetFromJsonAsync<JsonWebKeySet>(
            "https://www.googleapis.com/oauth2/v3/certs");
        if (jwks is null) return (false, null, null);

        // 2. Try to validate
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = "https://accounts.google.com",
            ValidAudience = _cfg["OAuth:Google:ClientId"],
            IssuerSigningKeys = jwks.Keys,
            ValidateLifetime = true
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, parameters, out _);
            var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
            var name = principal.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
            return (true, email, name);
        }
        catch
        {
            return (false, null, null);
        }
    }
}