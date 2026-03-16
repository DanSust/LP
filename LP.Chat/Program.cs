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
    // ✅ Пробуем подключиться к Redis
    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    IConnectionMultiplexer? redisMultiplexer = null;
    var redisAvailable = false;

    try
    {
        redisMultiplexer = ConnectionMultiplexer.Connect(redisConnection);
        redisAvailable = redisMultiplexer.IsConnected;
        Log.Information("✅ Redis connected: {Endpoint}", redisMultiplexer.GetEndPoints().FirstOrDefault());
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "⚠️ Redis unavailable. Running WITHOUT cache (direct DB access).");
        redisAvailable = false;
    }
    // ✅ Добавляем fallback, если Redis не доступен
    builder.Services.AddSingleton<IConnectionMultiplexer?>(sp => redisMultiplexer);

    // SignalR с Redis Backplane
    // ✅ SignalR с условным Redis Backplane
    var signalRBuilder = builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = true;
        options.MaximumReceiveMessageSize = 64 * 1024;
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    });

    if (redisAvailable)
    {
        signalRBuilder.AddStackExchangeRedis(options =>
        {
            var config = ConfigurationOptions.Parse(redisConnection);
            config.ChannelPrefix = "ChatHub";
            config.AbortOnConnectFail = false;
            options.Configuration = config;
        });
        Log.Information("✅ SignalR Redis backplane enabled");
    }
    else
    {
        Log.Warning("⚠️ SignalR running WITHOUT Redis backplane (single server mode)");
    }

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
    // ✅ IMessageCache только если Redis доступен, иначе null
    if (redisAvailable)
    {
        builder.Services.AddSingleton<IMessageCache, RedisMessageCache>();
        Log.Information("✅ RedisMessageCache enabled");
    }
    else
    {
        // Не регистрируем IMessageCache вообще - будем работать напрямую с БД
        builder.Services.AddSingleton<IMessageCache, NullMessageCache>();
        Log.Warning("⚠️ No message cache - using direct DB access");
    }
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

    var hubConnection = builder.Configuration.GetConnectionString("chatHub") ?? "https://0.0.0.0:5000";
    //app.Run("https://127.0.0.1:5000");
    if (builder.Environment.IsDevelopment())
    {
        app.Run("https://127.0.0.1:5000");
    }
    else
    {
        app.Run(hubConnection);
    }
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