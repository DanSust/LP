using LP.Entity;
using LP.Entity.Store;
using LP.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace LP.Server.OAuth
{
    // DTO для данных от Telegram
    public record TelegramAuthDto(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("first_name")] string FirstName,
        [property: JsonPropertyName("last_name")] string? LastName,    // <-- nullable
        [property: JsonPropertyName("username")] string? Username,     // <-- nullable
        [property: JsonPropertyName("photo_url")] string? PhotoUrl,    // <-- nullable
        [property: JsonPropertyName("auth_date")] long AuthDate,
        [property: JsonPropertyName("hash")] string Hash
    );

    [AllowAnonymous]
    [ApiController]
    [Route("api/auth")]
    public class TelegramController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ApplicationContext _db;
        private readonly IAuthService _service;
        private readonly UserStore _store;

        // ВАЖНО: Bot Token из @BotFather
        private string BotToken => _config["OAuth:Telegram:BotToken"]!;

        public TelegramController(IConfiguration config, ApplicationContext context, IAuthService service, UserStore store)
        {
            _config = config;
            _db = context;
            _service = service;
            _store = store;
        }

        [AllowAnonymous]
        [HttpPost("telegram")]
        public async Task<IActionResult> TelegramAuth([FromBody] TelegramAuthDto data)
        {
            // 1. Проверка подписи данных (критически важно!)
            if (!VerifyTelegramData(data))
                return Unauthorized("Invalid signature");

            // 2. Проверка freshness (данные не старше 24 часов)
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now - data.AuthDate > 86400)
                return Unauthorized("Auth data expired");

            //var token = _service.GenerateToken(user);
            //_store.GetOrCreateAsync()
            // 3. Поиск или создание пользователя
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.ProviderId == data.Id.ToString());

            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Provider = "telegram",
                    ProviderId = data.Id.ToString(),
                    Username = data.Username,
                    Caption = data.FirstName + data.LastName,
                    Email = null
                    //AvatarUrl = data.PhotoUrl
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }

            // 4. Генерация JWT
            
            return Ok(new { Token = token, User = user });
        }

        private bool VerifyTelegramData(TelegramAuthDto data)
        {
            // Алгоритм проверки подписи Telegram
            var dataCheckString = $"auth_date={data.AuthDate}\n" +
                                 $"first_name={data.FirstName}\n" +
                                 $"id={data.Id}\n" +
                                 (data.LastName != null ? $"last_name={data.LastName}\n" : "") +
                                 (data.PhotoUrl != null ? $"photo_url={data.PhotoUrl}\n" : "") +
                                 (data.Username != null ? $"username={data.Username}\n" : "");

            // Удаляем последний \n если есть optional поля
            dataCheckString = dataCheckString.TrimEnd('\n');

            using var sha256 = SHA256.Create();
            var secretKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(BotToken));

            using var hmac = new HMACSHA256(secretKey);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
            var computedHashString = BitConverter.ToString(computedHash).Replace("-", "").ToLower();

            return computedHashString == data.Hash.ToLower();
        }
    }
}
