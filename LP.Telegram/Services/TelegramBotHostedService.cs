using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Extensions;
using LP.TelegramAuthBot.Configuration;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Hosting;

namespace LP.TelegramAuthBot.Services;

public class TelegramBotHostedService : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramBotHandler _botHandler;
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly BotConfiguration _config;

    public TelegramBotHostedService(
        ITelegramBotClient botClient,
        ITelegramBotHandler botHandler,
        ILogger<TelegramBotHostedService> logger,
        IOptions<BotConfiguration> config)
    {
        _botClient = botClient;
        _botHandler = botHandler;
        _logger = logger;
        _config = config.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Telegram Bot Service");

        //Telegram.Bot.Extensions.
        var botInfo = await _botClient.GetMe(cancellationToken);
        _logger.LogInformation("Bot @{Username} (id: {Id}) is running", botInfo.Username, botInfo.Id);

        if (_config.UseWebhook)
        {
            await SetWebhookAsync(cancellationToken);
        }
        else
        {
            StartPolling(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Telegram Bot Service");
        return Task.CompletedTask;
    }

    private async Task SetWebhookAsync(CancellationToken ct)
    {
        try
        {
            var webhookUrl = $"{_config.WebhookUrl}/webhook/{_config.WebhookSecret}";

            await _botClient.SetWebhook(
                url: webhookUrl,
                allowedUpdates: Array.Empty<UpdateType>(),
                cancellationToken: ct);

            _logger.LogInformation("Webhook set to {Url}", webhookUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set webhook");
            throw;
        }
    }

    private void StartPolling(CancellationToken ct)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            updateHandler: (bot, update, token) => _botHandler.HandleUpdateAsync(update, token),
            errorHandler: (bot, error, token) => _botHandler.HandleErrorAsync(error, token),
            receiverOptions: receiverOptions,
            cancellationToken: ct
        );

        _logger.LogInformation("Bot started in polling mode");
    }
}