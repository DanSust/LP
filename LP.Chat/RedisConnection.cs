using StackExchange.Redis;

namespace LP.Chat;

/// <summary>
/// Безопасная обёртка над Redis.
/// Если Redis недоступен, чат продолжает работать напрямую с БД,
/// а все операции с кэшем/backplane просто пропускаются.
/// </summary>
public sealed class RedisConnection
{
    private readonly IConnectionMultiplexer? _multiplexer;
    private readonly ILogger<RedisConnection> _logger;

    public RedisConnection(IConnectionMultiplexer? multiplexer, ILogger<RedisConnection> logger)
    {
        _multiplexer = multiplexer;
        _logger = logger;
    }

    public bool IsAvailable => _multiplexer?.IsConnected == true;

    public IConnectionMultiplexer? Multiplexer => IsAvailable ? _multiplexer : null;

    public IDatabase? TryGetDatabase()
    {
        if (!IsAvailable)
        {
            return null;
        }

        try
        {
            return _multiplexer!.GetDatabase();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis database is unavailable");
            return null;
        }
    }

    public ISubscriber? TryGetSubscriber()
    {
        if (!IsAvailable)
        {
            return null;
        }

        try
        {
            return _multiplexer!.GetSubscriber();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis subscriber is unavailable");
            return null;
        }
    }
}
