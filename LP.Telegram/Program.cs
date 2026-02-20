using LP.TelegramAuthBot.Configuration;
using LP.TelegramAuthBot.Services;
using Microsoft.Extensions.Options;
using Telegram.Bot;

class Program
{
    private const string ApiVersion = "v1";
    private const string ApiName = "StatMonitoring API";

    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

// Настройка конфигурации бота
        builder.Services.Configure<BotConfiguration>(
            builder.Configuration.GetSection("BotConfiguration"));

// Регистрация HTTP клиента для взаимодействия с твоим API
        builder.Services.AddHttpClient<ITelegramAuthClient, TelegramAuthClient>(client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["BackendApi:BaseUrl"]!);
            client.DefaultRequestHeaders.Add("User-Agent", "TelegramAuthBot/1.0");
        });

// Регистрация сервисов бота
        builder.Services.AddSingleton<ITelegramBotHandler, TelegramBotHandler>();
        builder.Services.AddSingleton<TelegramBotHostedService>();

// Добавляем hosted service для запуска бота
        builder.Services.AddHostedService<TelegramBotHostedService>();

        builder.Services.AddSingleton<ITelegramBotClient>(provider =>
        {
            var botConfig = provider.GetRequiredService<IOptions<BotConfiguration>>().Value;
            return new TelegramBotClient(botConfig.Token);
        });

        // Swagger для отладки
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

// Health check для мониторинга
        app.MapGet("/health", () => Results.Ok(new {status = "healthy", timestamp = DateTime.UtcNow}));

        app.Run();
    }
}