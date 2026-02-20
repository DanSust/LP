namespace LP.TelegramAuthBot.Models;

public class TelegramUser
{
    public long Id { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}