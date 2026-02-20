namespace LP.TelegramAuthBot.Services;

public static class BotCommandParser
{
    public static (string Command, string? Argument) Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (string.Empty, null);

        var parts = text.Trim().Split(' ', 2);
        var command = parts[0].ToLower().TrimStart('/');

        return (command, parts.Length > 1 ? parts[1] : null);
    }
}