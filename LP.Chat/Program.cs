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

    // ✅ Redis опционален: без него чат работает напрямую с БД и in-memory SignalR.
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    IConnectionMultiplexer? redisMultiplexer = null;

    if (!string.IsNullOrWhiteSpace(redisConnection))
    {
        try
        {
            var redisConfig = ConfigurationOptions.Parse(redisConnection);
            // Не падаем и не рвём соединения SignalR, если Redis лежит.
            redisConfig.AbortOnConnectFail = false;
            redisConfig.ConnectTimeout = 2000;
            redisConfig.SyncTimeout = 2000;
            redisConfig.ConnectRetry = 1;

            redisMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisConfig);

            if (redisMultiplexer.IsConnected)
            {
                Log.Information("✅ Redis connected: {Endpoint}", redisMultiplexer.GetEndPoints().FirstOrDefault());
            }
            else
            {
                Log.Warning("⚠️ Redis unavailable. Chat will work directly with the database.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ Redis unavailable. Chat will work directly with the database.");
            redisMultiplexer = null;
        }
    }
    else
    {
        Log.Warning("⚠️ Redis connection string is not configured. Chat will work directly with the database.");
    }

    // Единая безопасная точка доступа к Redis (никогда не бросает исключение при использовании).
    builder.Services.AddSingleton(sp => new RedisConnection(
        redisMultiplexer,
        sp.GetRequiredService<ILogger<RedisConnection>>()));

    // SignalR: по умолчанию in-memory (одна нода чата).
    // Backplane нужен только при нескольких экземплярах чата — включается флагом Chat:UseRedisBackplane.
    var signalRBuilder = builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = true;
        options.MaximumReceiveMessageSize = 64 * 1024;
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    });

    var useRedisBackplane = builder.Configuration.GetValue("Chat:UseRedisBackplane", false);
    if (useRedisBackplane && redisMultiplexer?.IsConnected == true)
    {
        signalRBuilder.AddStackExchangeRedis(options =>
        {
            var config = ConfigurationOptions.Parse(redisConnection!);
            config.ChannelPrefix = RedisChannel.Literal("ChatHub");
            config.AbortOnConnectFail = false;
            options.Configuration = config;
        });
        Log.Information("✅ SignalR Redis backplane enabled");
    }
    else
    {
        Log.Information("✅ SignalR running in single-server mode (in-memory), Redis backplane disabled");
    }

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
    // Кэш сообщений безопасен: при недоступном Redis читает/пишет напрямую в БД.
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
    app.MapGet("/health", (RedisConnection redis) =>
    {
        var connections = ChatHub.GetActiveConnections();
        var redisConnected = redis.IsAvailable;

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
            RedisEndpoint = redis.Multiplexer?.GetEndPoints()?.FirstOrDefault()?.ToString() ?? "Not connected"
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

    Console.WriteLine("Chat");

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