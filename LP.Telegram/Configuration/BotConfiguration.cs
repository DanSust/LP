namespace LP.TelegramAuthBot.Configuration;

public class BotConfiguration
{
    /// <summary>
    /// Bot token from @BotFather
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Webhook URL for receiving updates (if UseWebhook is true)
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Secret path for webhook endpoint (to prevent unauthorized access)
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// Use webhook mode instead of polling
    /// </summary>
    public bool UseWebhook { get; set; } = false;

    /// <summary>
    /// Rate limit for messages per user (per minute)
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 10;

    /// <summary>
    /// Session code expiration time in minutes
    /// </summary>
    public int SessionCodeExpirationMinutes { get; set; } = 5;
}