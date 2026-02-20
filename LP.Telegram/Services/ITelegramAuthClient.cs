namespace LP.TelegramAuthBot.Services
{
    public interface ITelegramAuthClient
    {
        Task<bool> LinkTelegramAccountAsync(string code, long telegramUserId, string? username, CancellationToken ct = default);
        Task<bool> CheckCodeValidAsync(string code, CancellationToken ct = default);
    }
}
