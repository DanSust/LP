using LP.Chat;
using LP.Chat.Interfaces;
using LP.Entity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using System.Security.Claims;
using System.Threading.RateLimiting;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ✅ ОТКЛЮЧАЕМ требование авторизации по умолчанию
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = null; // Никакая авторизация не требуется
    });

    // ✅ Redis Configuration
    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    // ✅ Добавляем fallback, если Redis не доступен
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        try
        {
            return ConnectionMultiplexer.Connect(redisConnection);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Redis connection failed. Running without cache.");
            return null; // Или создайте mock-реализацию
        }
    });

    // SignalR с Redis Backplane
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = false;
        options.MaximumReceiveMessageSize = 64 * 1024;
    })
    .AddStackExchangeRedis(options =>
    {
        // ✅ Правильный способ: создаем ConfigurationOptions и присваиваем
        var config = ConfigurationOptions.Parse(redisConnection);
        config.ChannelPrefix = "ChatHub"; // Префикс для каналов Redis
        config.AbortOnConnectFail = false; // Важно для продакшена

        options.Configuration = config;
    });

    // Redis для кэша
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        ConnectionMultiplexer.Connect(redisConnection));

    // Rate Limiting (100 сообщений в минуту на пользователя)
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("per_user", context =>
        {
            // Получаем userId из токена или Query String
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? context.Request.Query["userId"].ToString()
                         ?? "anonymous";

            return RateLimitPartition.GetTokenBucketLimiter(userId, key => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 100,
                AutoReplenishment = true
            });
        });

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = 429;
            await context.HttpContext.Response.WriteAsync("Too many requests");
        };
    });

    // Сервисы
    builder.Services.AddSingleton<IMessageBuffer, BufferedMessageStore>();
    builder.Services.AddSingleton<IMessageCache, RedisMessageCache>();
    builder.Services.AddSingleton<IUserPresenceService, UserPresenceService>();
    builder.Services.AddSingleton<IMessageBotService, MessageBotService>();

    // DB Context
    builder.Services.AddDbContext<ApplicationContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure()
        )
    );

    var app = builder.Build();

    app.UseRateLimiter();
    app.MapHub<ChatHub>("/chat");
    app.MapGet("/health", (IConnectionMultiplexer redis) =>
    {
        var connections = ChatHub.GetActiveConnections();
        var redisConnected = redis?.IsConnected ?? false;

        return Results.Json(new
        {
            Status = "OK",
            Timestamp = DateTime.UtcNow,
            ActiveConnections = connections.Count,
            Connections = connections.Select(c => new
            {
                UserId = c.UserId,
                ChatId = c.ChatId,
                ConnectedAt = c.ConnectedAt,
                ConnectionDuration = DateTime.UtcNow - c.ConnectedAt
            }).OrderByDescending(c => c.ConnectedAt),
            RedisConnected = redisConnected,
            RedisEndpoint = redis?.GetEndPoints()?.FirstOrDefault()?.ToString() ?? "Not connected"
        });
    });

    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    // ✅ ВОТ ЗДЕСЬ вызываем GracefulShutdown
    lifetime.ApplicationStopping.Register(() =>
    {
        Log.Warning("Shutting down... Flushing message buffer...");
        var buffer = app.Services.GetRequiredService<IMessageBuffer>();
        buffer.ForceFlushAsync().GetAwaiter().GetResult();
        Log.Warning("Shutdown complete");
    });

    app.Run("https://localhost:5000");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;