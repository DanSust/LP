using LP.TelegramAuthBot.Services;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace LP.TelegramAuthBot.Services;

public class TelegramAuthClient : ITelegramAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramAuthClient> _logger;
    private readonly string _apiKey;

    public TelegramAuthClient(
        HttpClient httpClient,
        ILogger<TelegramAuthClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["BackendApi:ApiKey"]!;
    }

    public async Task<bool> LinkTelegramAccountAsync(
        string code,
        long telegramUserId,
        string? username,
        CancellationToken ct = default)
    {
        try
        {
            var request = new
            {
                Code = code,
                TelegramUserId = telegramUserId,
                Username = username,
                Timestamp = DateTime.UtcNow
            };

            var response = await _httpClient.PostAsJsonAsync(
                "auth/telegram/link",
                request,
                ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully linked telegram user {TelegramUserId} with code {Code}",
                    telegramUserId, code);
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Failed to link telegram account. Code: {Code}, Status: {Status}, Error: {Error}",
                code, response.StatusCode, error);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking telegram account for code {Code}", code);
            return false;
        }
    }

    public async Task<bool> CheckCodeValidAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"auth/telegram/code/{code}/valid",
                ct);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking code validity for {Code}", code);
            return false;
        }
    }
}