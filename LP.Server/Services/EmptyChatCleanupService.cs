using LP.Entity;
using Microsoft.EntityFrameworkCore;

namespace LP.Server.Services;

/// <summary>
/// Удаляет пустые чаты, которые не получили ни одного сообщения за 7 дней.
/// Это поведение соответствует подсказке в списке чатов.
/// </summary>
public class EmptyChatCleanupService : BackgroundService
{
    // Должно совпадать с EMPTY_CHAT_TTL_MS на клиенте (chat-expiry.ts).
    private static readonly TimeSpan EmptyChatTtl = TimeSpan.FromDays(7);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmptyChatCleanupService> _logger;

    public EmptyChatCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<EmptyChatCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete empty chats");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var cutoff = DateTime.Now - EmptyChatTtl;

        var deleted = await db.Chats
            .Where(chat => chat.Time < cutoff && !db.Messages.Any(message => message.ChatId == chat.Id))
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Deleted {Count} empty chats older than {Days} days",
                deleted,
                EmptyChatTtl.TotalDays);
        }
    }
}
