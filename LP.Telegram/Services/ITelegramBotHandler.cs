using Telegram.Bot.Types;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Update = Telegram.Bot.Types.Update;

namespace LP.TelegramAuthBot.Services;

public interface ITelegramBotHandler
{
    Task HandleUpdateAsync(Update update, CancellationToken ct);
    Task HandleErrorAsync(Exception exception, CancellationToken ct);
}