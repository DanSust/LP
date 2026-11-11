using LP.Chat;
using LP.Chat.Interfaces;
using LP.Entity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

public class RedisMessageCache : IMessageCache
{
    private readonly RedisConnection _redis;
    private readonly IServiceProvider _services; // ✅ Вместо ApplicationContext
    private readonly ILogger<RedisMessageCache> _logger;
    private const string HistoryKeyPrefix = "chat:history:";

    public RedisMessageCache(RedisConnection redis, IServiceProvider services, ILogger<RedisMessageCache> logger)
    {
        _redis = redis;
        _services = services; // ✅ Получаем IServiceProvider
        _logger = logger;
    }

    public async Task<List<Message>> GetHistoryAsync(Guid chatId, int page = 0, int pageSize = 50)
    {
        var key = HistoryKeyPrefix + chatId;
        var db = _redis.TryGetDatabase();

        if (db is not null)
        {
            try
            {
                var cached = await db.ListRangeAsync(key, page * pageSize, (page + 1) * pageSize - 1);

                if (cached.Length > 0)
                {
                    return cached
                        .Select(x => JsonSerializer.Deserialize<Message>(x.ToString()))
                        .Where(m => m is not null)
                        .Cast<Message>()
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                // Redis недоступен или в кэше битые данные — читаем напрямую из БД.
                _logger.LogWarning(ex, "Failed to read chat history from Redis, falling back to database");
            }
        }

        // ✅ Создаем Scope "на лету"
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var history = await dbContext.Messages
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.Time)
            .Take(200)
            .ToListAsync();

        if (db is not null)
        {
            try
            {
                var json = history.Select(h => (RedisValue)JsonSerializer.SerializeToUtf8Bytes(h)).ToArray();
                await db.ListLeftPushAsync(key, json);
                await db.KeyExpireAsync(key, TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                // Кэширование не критично — просто логируем.
                _logger.LogWarning(ex, "Failed to cache chat history in Redis");
            }
        }

        return history.Take(pageSize).ToList();
    }

    public async Task AddToCacheAsync(Message message)
    {
        var db = _redis.TryGetDatabase();
        if (db is null)
        {
            // Redis недоступен — сообщение уже сохраняется в БД через IMessageBuffer.
            return;
        }

        try
        {
            var key = HistoryKeyPrefix + message.ChatId;

            var json = JsonSerializer.SerializeToUtf8Bytes(message);
            await db.ListLeftPushAsync(key, json);
            await db.ListTrimAsync(key, 0, 199);
            await db.KeyExpireAsync(key, TimeSpan.FromHours(24));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add message to Redis cache");
        }
    }
}
