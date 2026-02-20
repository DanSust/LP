namespace LP.Entity
{
    public enum Reason {rsBlock = 0, rsSpam = 1, rsAbuse }
    public class Reject : BaseEntity
    {
        public string Caption { get; set; } = string.Empty;
        public Guid Owner { get; set; }
        public Guid UserId { get; set; }
        public Reason Reason { get; set; }
        public DateTime Time { get; set; } = DateTime.UtcNow;
    }
}