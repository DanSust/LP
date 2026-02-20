namespace LP.TelegramAuthBot.Models
{
    public class AuthSessionDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool IsLinked { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
