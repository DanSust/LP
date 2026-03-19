namespace LP.Server.DTO
{
    public class RatingDto
    {
        public Guid UserId { get; set; }
        public double Rating { get; set; } // 0-10
        public RatingComponents Components { get; set; } = new();
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RatingComponents
    {
        public double ActivityScore { get; set; } // 0-10
        public double ResponsivenessScore { get; set; } // 0-10
        public double InterestsScore { get; set; } // 0-10

        // Детали для отладки/отображения
        public ActivityDetails Activity { get; set; } = new();
        public ResponsivenessDetails Responsiveness { get; set; } = new();
        public InterestsDetails Interests { get; set; } = new();
    }

    public class ActivityDetails
    {
        public int DaysSinceLastLogin { get; set; }
        public int AccountAgeDays { get; set; }
        public int DaysSinceLastEvent { get; set; }
        public double Score { get; set; }
    }

    public class ResponsivenessDetails
    {
        public int TotalMessages { get; set; }
        public double? AverageResponseTimeMinutes { get; set; }
        public double? ResponseRate { get; set; }
        public int DaysSinceLastMessage { get; set; }
        public int TotalChats { get; set; }
        public int ActiveChats { get; set; }
        public double AverageMessagesPerChat { get; set; }
        public double Score { get; set; }
    }

    public class InterestsDetails
    {
        public int TotalInterests { get; set; }
        public int UniqueGroups { get; set; }
        public List<string> InterestNames { get; set; } = new();
        public double Score { get; set; }
    }
}