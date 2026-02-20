using LP.Chat.Interfaces;
using LP.Entity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

public class RedisMessageCache : IMessageCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceProvider _services; // ✅ Вместо ApplicationContext
    private readonly ILogger<RedisMessageCache> _logger;
    private const string HistoryKeyPrefix = "chat:history:";

    public RedisMessageCache(IConnectionMultiplexer redis, IServiceProvider services, ILogger<RedisMessageCache> logger)
    {
        _redis = redis;
        _services = services; // ✅ Получаем IServiceProvider
        _logger = logger;
    }

    public async Task<List<Message>> GetHistoryAsync(Guid chatId, int page = 0, int pageSize = 50)
    {
        var db = _redis.GetDatabase();
        var key = HistoryKeyPrefix + chatId;

        var cached = await db.ListRangeAsync(key, page * pageSize, (page + 1) * pageSize - 1);

        if (cached.Length > 0)
        {
            return cached.Select(x => JsonSerializer.Deserialize<Message>(x)).ToList();
        }

        // ✅ Создаем Scope "на лету"
        using var scope = _services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var history = await dbContext.Messages
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.Time)
            .Take(200)
            .ToListAsync();

        var json = history.Select(h => (RedisValue)JsonSerializer.SerializeToUtf8Bytes(h)).ToArray();
        await db.ListLeftPushAsync(key, json);
        await db.KeyExpireAsync(key, TimeSpan.FromHours(24));

        return history.Take(pageSize).ToList();
    }

    public async Task AddToCacheAsync(Message message)
    {
        try
        {
            var db = _redis.GetDatabase();
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