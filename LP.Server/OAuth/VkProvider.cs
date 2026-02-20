using LP.Entity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LP.Server.OAuth;
public record VkTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("id_token")] string? IdToken
);

public record VkUserInfoResponse(
    [property: JsonPropertyName("user")] VkUser User
);

public record VkUser(
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string LastName,
    [property: JsonPropertyName("avatar")] string? Avatar,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("phone")] string? Phone
);

public sealed class VkProvider : IOAuthProvider
{
    private readonly IConfiguration _cfg;
    private readonly HttpClient _http;
    private IMemoryCache _cache { get; set; }

    public string Name => "VK";

    IMemoryCache IOAuthProvider._cache { get => _cache; set => _cache = value; }

    public VkProvider(IHttpClientFactory f, IConfiguration c, IMemoryCache m)
    {
        _cfg = c;
        _http = f.CreateClient();
        _cache = m;
    }

    private string GenerateCodeVerifier()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[64]; // 64 символа — оптимальный размер
        rng.GetBytes(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private string GenerateCodeChallenge(string verifier)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(verifier));
        return Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(hash);
    }

    public string BuildAuthUrl(string state)
    {
        var codeChallenge = GenerateCodeChallenge(GenerateCodeVerifier());
        return $"https://id.vk.com/authorize?" +
            $"client_id={_cfg["OAuth:VK:ClientId"]}" +
            $"&redirect_uri={Uri.EscapeDataString(_cfg["OAuth:VK:RedirectUri"])}" +
            $"&response_type=code" +
            $"&scope=email" +
            $"&state={state}" +
            $"&code_challenge_method=s256" +
            $"&code_challenge={codeChallenge}"; // Используем state как code_challenge для упрощения
    }

    public async Task<TokenResponse> ExchangeCodeAsync(string code)
    {
        var dict = new Dictionary<string, string>
        {
            ["client_id"] = _cfg["OAuth:VK:ClientId"],
            ["client_secret"] = _cfg["OAuth:VK:ClientSecret"],
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = _cfg["OAuth:VK:RedirectUri"],
            ["code_verifier"] = _cfg["OAuth:VK:CodeVerifier"] ?? code // Если нужен PKCE
        };

        using var res = await _http.PostAsync(
            "https://id.vk.com/oauth2/auth",
            new FormUrlEncodedContent(dict));

        res.EnsureSuccessStatusCode();
        var vkResponse = await res.Content.ReadFromJsonAsync<VkTokenResponse>();

        // Конвертируем в общий формат TokenResponse
        return new TokenResponse(
            AccessToken: vkResponse.AccessToken,
            RefreshToken: vkResponse.RefreshToken,
            ExpiresIn: vkResponse.ExpiresIn,
            TokenType: vkResponse.TokenType,
            Scope: "email",
            IdToken: vkResponse.IdToken ?? string.Empty
        );
    }

    public async Task<User> GetUserInfoAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            "https://id.vk.com/oauth2/user_info");
        req.Headers.Authorization = new("Bearer", accessToken);

        using var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();

        var vkUserInfo = await res.Content.ReadFromJsonAsync<VkUserInfoResponse>();

        if (vkUserInfo?.User == null)
            throw new Exception("Failed to retrieve VK user info");

        return new User
        {
            Id = Guid.NewGuid(),
            Email = vkUserInfo.User.Email ?? $"vk_{vkUserInfo.User.UserId}@vk.local",
            Username = $"{vkUserInfo.User.FirstName} {vkUserInfo.User.LastName}",
            Created = DateTime.UtcNow,
            // Дополнительные поля можно заполнить здесь
            // Avatar = vkUserInfo.User.Avatar,
            // PhoneNumber = vkUserInfo.User.Phone
        };
    }

    public async Task<(bool ok, string? email, string? name)> ValidateAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, null, null);

        try
        {
            // VK ID использует JWT токены
            var handler = new JwtSecurityTokenHandler();

            // Получаем публичные ключи VK
            var jwks = await GetVkPublicKeysAsync();
            if (jwks == null)
                return (false, null, null);

            var parameters = new TokenValidationParameters
            {
                ValidIssuer = "https://id.vk.com",
                ValidAudience = _cfg["OAuth:VK:ClientId"],
                IssuerSigningKeys = jwks.Keys,
                ValidateLifetime = true,
                ValidateIssuer = true,
                ValidateAudience = true
            };

            var principal = handler.ValidateToken(token, parameters, out _);

            // Извлекаем claims
            var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
            var name = principal.FindFirst(JwtRegisteredClaimNames.Name)?.Value
                ?? principal.FindFirst("given_name")?.Value;

            return (true, email, name);
        }
        catch (Exception ex)
        {
            // Логируем ошибку если нужно
            Console.WriteLine($"VK token validation failed: {ex.Message}");
            return (false, null, null);
        }
    }

    private async Task<JsonWebKeySet?> GetVkPublicKeysAsync()
    {
        try
        {
            // Кешируем ключи на 24 часа
            const string cacheKey = "vk_jwks";

            if (_cache.TryGetValue(cacheKey, out JsonWebKeySet? cachedKeys))
                return cachedKeys;

            var jwks = await _http.GetFromJsonAsync<JsonWebKeySet>(
                "https://id.vk.com/.well-known/jwks.json");

            if (jwks != null)
            {
                _cache.Set(cacheKey, jwks, TimeSpan.FromHours(24));
            }

            return jwks;
        }
        catch
        {
            return null;
        }
    }

    // Дополнительный метод для получения информации о пользователе через API
    public async Task<VkUser?> GetUserProfileAsync(string accessToken, long userId)
    {
        try
        {
            var url = $"https://api.vk.com/method/users.get?" +
                     $"user_ids={userId}" +
                     $"&fields=photo_200,email" +
                     $"&access_token={accessToken}" +
                     $"&v=5.131";

            using var res = await _http.GetAsync(url);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            var response = json.GetProperty("response");

            if (response.GetArrayLength() > 0)
            {
                var user = response[0];
                return new VkUser(
                    UserId: user.GetProperty("id").ToString(),
                    FirstName: user.GetProperty("first_name").GetString() ?? "",
                    LastName: user.GetProperty("last_name").GetString() ?? "",
                    Avatar: user.TryGetProperty("photo_200", out var photo) ? photo.GetString() : null,
                    Email: user.TryGetProperty("email", out var email) ? email.GetString() : null,
                    Phone: null
                );
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}