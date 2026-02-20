using LP.TelegramAuthBot.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = Telegram.Bot.Types.Message;
using Update = Telegram.Bot.Types.Update;

namespace LP.TelegramAuthBot.Services;

public class TelegramBotHandler : ITelegramBotHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramAuthClient _authClient;
    private readonly ILogger<TelegramBotHandler> _logger;

    public TelegramBotHandler(
        ITelegramBotClient botClient,
        ITelegramAuthClient authClient,
        ILogger<TelegramBotHandler> logger)
    {
        _botClient = botClient;
        _authClient = authClient;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        if (update.Message is not { } message)
            return;

        if (message.Text is not { } messageText)
            return;

        var user = MapTelegramUser(message.From);
        _logger.LogInformation(
            "Received message from {Username} ({UserId}): {Text}",
            user.Username, user.Id, messageText);

        try
        {
            var (command, argument) = BotCommandParser.Parse(messageText);

            await (command switch
            {
                "start" => HandleStartCommand(message, argument, ct),
                _ => HandleAuthCode(message, messageText.Trim(), ct)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from {UserId}", user.Id);
            await SendErrorMessage(message.Chat.Id, ct);
        }
    }

    private async Task HandleStartCommand(Message message, string? argument, CancellationToken ct)
    {
        var welcomeText = argument != null
            ? $@"👋 Добро пожаловать!
Отправь мне код авторизации, чтобы войти в приложение."
            : $@"👋 Добро пожаловать! 

Чтобы авторизоваться на сайте:
1. Нажми кнопку ""Войти через Telegram""
2. Отправь мне полученный код

Можешь просто скопировать и вставить его сюда.";

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: welcomeText,
            parseMode: ParseMode.Markdown,
            cancellationToken: ct);
    }

    private async Task HandleAuthCode(Message message, string code, CancellationToken ct)
    {
        // Валидация формата кода
        if (code.Length != 6 || !code.All(char.IsLetterOrDigit))
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "❌ Неверный формат кода. Код должен состоять из 6 символов (буквы и цифры).",
                cancellationToken: ct);
            return;
        }

        // Проверяем код через API
        var isValid = await _authClient.CheckCodeValidAsync(code, ct);
        if (!isValid)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "❌ Код недействителен или истек. Попробуйте получить новый код на сайте.",
                cancellationToken: ct);
            return;
        }

        // Привязываем аккаунт
        var user = MapTelegramUser(message.From!);
        var success = await _authClient.LinkTelegramAccountAsync(
            code,
            user.Id,
            user.Username,
            ct);

        if (success)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $@"✅ Успешная авторизации!
Телеграм аккаунт **@{user.Username ?? "скрыт"}** привязан к вашему профилю.

Можете закрыть это окно и вернуться на сайт.",
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
        }
        else
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "❌ Ошибка привязки аккаунта. Попробуйте еще раз или обратитесь в поддержку.",
                cancellationToken: ct);
        }
    }

    private static TelegramUser MapTelegramUser(User from)
    {
        return new TelegramUser
        {
            Id = from.Id,
            Username = from.Username,
            FirstName = from.FirstName,
            LastName = from.LastName
        };
    }

    private async Task SendErrorMessage(long chatId, CancellationToken ct)
    {
        try
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "😔 Произошла ошибка. Пожалуйста, попробуйте позже.",
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error message");
        }
    }

    public Task HandleErrorAsync(Exception exception, CancellationToken ct)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error:\n[{apiEx.ErrorCode}]\n{apiEx.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(exception, "Telegram bot error: {Message}", errorMessage);
        return Task.CompletedTask;
    }
}