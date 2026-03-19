using LP.Entity;
using LP.Server.DTO;
using LP.Server.Services.Rating;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace LP.Server.Services.Rating
{
    public class RatingService : IRatingService
    {
        private readonly ApplicationContext _context;
        private readonly IDistributedCache _cache;
        private readonly ILogger<RatingService> _logger;
        private const string CACHE_KEY_PREFIX = "user_rating_";
        private readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(1);

        // Веса для компонентов рейтинга
        private const double ACTIVITY_WEIGHT = 0.3;        // 30%
        private const double RESPONSIVENESS_WEIGHT = 0.5;  // 50%
        private const double INTERESTS_WEIGHT = 0.2;       // 20%

        public RatingService(
            ApplicationContext context,
            IDistributedCache cache,
            ILogger<RatingService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<RatingDto> CalculateUserRating(Guid userId)
        {
            try
            {
                // Проверяем кеш
                var cached = await GetCachedRating(userId);
                if (cached > 0)
                {
                    return new RatingDto
                    {
                        UserId = userId,
                        Rating = cached,
                        CalculatedAt = DateTime.UtcNow
                    };
                }

                // Получаем данные пользователя
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    throw new ArgumentException($"User {userId} not found");

                // Получаем статистику сообщений
                var messageStats = await GetMessageStats(userId);

                // Получаем статистику чатов
                var chatStats = await GetChatStats(userId);

                // Получаем интересы пользователя
                var interests = await GetUserInterests(userId);

                // Рассчитываем компоненты
                var activityScore = CalculateActivityScore(user, out var activityDetails);
                var responsivenessScore = CalculateResponsivenessScore(messageStats, chatStats, out var responsivenessDetails);
                var interestsScore = CalculateInterestsScore(interests, out var interestsDetails);

                // Итоговый рейтинг с весами
                var totalRating = Math.Round(
                    (activityScore * ACTIVITY_WEIGHT) +
                    (responsivenessScore * RESPONSIVENESS_WEIGHT) +
                    (interestsScore * INTERESTS_WEIGHT), 1);

                var result = new RatingDto
                {
                    UserId = userId,
                    Rating = Math.Max(0, Math.Min(10, totalRating)),
                    Components = new RatingComponents
                    {
                        ActivityScore = Math.Round(activityScore, 1),
                        ResponsivenessScore = Math.Round(responsivenessScore, 1),
                        InterestsScore = Math.Round(interestsScore, 1),
                        Activity = activityDetails,
                        Responsiveness = responsivenessDetails,
                        Interests = interestsDetails
                    },
                    CalculatedAt = DateTime.UtcNow
                };

                // Сохраняем в кеш
                await CacheRating(userId, result.Rating);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating rating for user {UserId}", userId);
                throw;
            }
        }

        public async Task<double> GetCachedRating(Guid userId)
        {
            try
            {
                var cached = await _cache.GetStringAsync($"{CACHE_KEY_PREFIX}{userId}");
                if (!string.IsNullOrEmpty(cached) && double.TryParse(cached, out var rating))
                {
                    return rating;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cached rating for user {UserId}", userId);
            }
            return 0;
        }

        public async Task InvalidateCache(Guid userId)
        {
            await _cache.RemoveAsync($"{CACHE_KEY_PREFIX}{userId}");
        }

        private async Task CacheRating(Guid userId, double rating)
        {
            try
            {
                await _cache.SetStringAsync(
                    $"{CACHE_KEY_PREFIX}{userId}",
                    rating.ToString(),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = CACHE_DURATION
                    });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache rating for user {UserId}", userId);
            }
        }

        private async Task<MessageStats> GetMessageStats(Guid userId)
        {
            var stats = await _context.Messages
                .Where(m => m.UserId == userId)
                .GroupBy(m => 1)
                .Select(g => new MessageStats
                {
                    TotalMessages = g.Count(),
                    LastMessageDate = g.Max(m => m.Time),
                    AverageResponseTime = CalculateAverageResponseTime(userId) // Будет реализовано отдельно
                })
                .FirstOrDefaultAsync();

            return stats ?? new MessageStats();
        }

        private async Task<ChatStats> GetChatStats(Guid userId)
        {
            var chats = await _context.Chats
                .Where(c => c.Owner == userId || c.UserId == userId)
                .Select(c => new
                {
                    c.Id,
                    MessageCount = _context.Messages.Count(m => m.ChatId == c.Id)
                })
                .ToListAsync();

            return new ChatStats
            {
                TotalChats = chats.Count,
                ActiveChats = chats.Count(c => c.MessageCount > 0),
                AverageMessagesPerChat = chats.Any() ? chats.Average(c => c.MessageCount) : 0
            };
        }

        private async Task<List<UserInterestData>> GetUserInterests(Guid userId)
        {
            return await _context.UserInterests
                .Where(ui => ui.User.Id == userId)
                .OrderBy(ui => ui.Order)
                .Select(ui => new UserInterestData
                {
                    Id = ui.Interest.Id,
                    Name = ui.Interest.Name,
                    Group = ui.Interest.Group
                })
                .ToListAsync();
        }

        private double? CalculateAverageResponseTime(Guid userId)
        {
            // Сложная логика для расчета среднего времени ответа
            // Можно реализовать позже
            return null;
        }

        private double CalculateActivityScore(User user, out ActivityDetails details)
        {
            var now = DateTime.UtcNow;
            double score = 5.0; // Базовый балл

            details = new ActivityDetails();

            // Последний вход (LastLogin)
            if (user.LastLogin.HasValue)
            {
                details.DaysSinceLastLogin = (int)(now - user.LastLogin.Value).TotalDays;

                if (details.DaysSinceLastLogin <= 1)
                    score += 3.0;
                else if (details.DaysSinceLastLogin <= 3)
                    score += 1.5;
                else if (details.DaysSinceLastLogin <= 7)
                    score += 0.5;
                else if (details.DaysSinceLastLogin > 30)
                    score -= 2.0;
            }
            else
            {
                score -= 1.0;
            }

            // Возраст аккаунта (Created)
            if (user.Created.HasValue)
            {
                details.AccountAgeDays = (int)(now - user.Created.Value).TotalDays;

                if (details.AccountAgeDays > 365)
                    score += 2.0;
                else if (details.AccountAgeDays > 180)
                    score += 1.0;
                else if (details.AccountAgeDays > 30)
                    score += 0.5;
                else if (details.AccountAgeDays < 7)
                    score -= 0.5;
            }

            // Просмотр событий (EventsSeen)
            if (user.EventsSeen.HasValue)
            {
                details.DaysSinceLastEvent = (int)(now - user.EventsSeen.Value).TotalDays;

                if (details.DaysSinceLastEvent <= 3)
                    score += 1.0;
            }

            details.Score = Math.Max(0, Math.Min(10, score));
            return details.Score;
        }

        private double CalculateResponsivenessScore(MessageStats messageStats, ChatStats chatStats, out ResponsivenessDetails details)
        {
            double score = 5.0; // Базовый балл
            details = new ResponsivenessDetails();

            // Статистика сообщений
            details.TotalMessages = messageStats.TotalMessages;

            if (messageStats.TotalMessages > 1000)
                score += 3.0;
            else if (messageStats.TotalMessages > 500)
                score += 2.0;
            else if (messageStats.TotalMessages > 100)
                score += 1.0;
            else if (messageStats.TotalMessages > 10)
                score += 0.5;
            else if (messageStats.TotalMessages < 5)
                score -= 1.0;

            // Время ответа (если есть данные)
            if (messageStats.AverageResponseTime.HasValue)
            {
                details.AverageResponseTimeMinutes = messageStats.AverageResponseTime.Value;

                if (messageStats.AverageResponseTime < 5)
                    score += 2.0;
                else if (messageStats.AverageResponseTime < 30)
                    score += 1.0;
                else if (messageStats.AverageResponseTime < 120)
                    score += 0.0;
                else if (messageStats.AverageResponseTime < 1440)
                    score -= 1.0;
                else
                    score -= 2.0;
            }

            // Процент ответов (если есть данные)
            if (messageStats.ResponseRate.HasValue)
            {
                details.ResponseRate = messageStats.ResponseRate.Value;

                if (messageStats.ResponseRate > 90)
                    score += 2.0;
                else if (messageStats.ResponseRate > 70)
                    score += 1.0;
                else if (messageStats.ResponseRate < 30)
                    score -= 1.0;
            }

            // Давность последнего сообщения
            if (messageStats.LastMessageDate.HasValue)
            {
                details.DaysSinceLastMessage = (int)(DateTime.UtcNow - messageStats.LastMessageDate.Value).TotalDays;

                if (details.DaysSinceLastMessage <= 1)
                    score += 1.0;
                else if (details.DaysSinceLastMessage > 14)
                    score -= 1.0;
            }

            // Статистика чатов
            details.TotalChats = chatStats.TotalChats;
            details.ActiveChats = chatStats.ActiveChats;
            details.AverageMessagesPerChat = chatStats.AverageMessagesPerChat;

            if (chatStats.TotalChats > 20)
                score += 1.0;

            if (chatStats.ActiveChats > 5)
                score += 1.0;

            if (chatStats.AverageMessagesPerChat > 50)
                score += 1.0;

            details.Score = Math.Max(0, Math.Min(10, score));
            return details.Score;
        }

        private double CalculateInterestsScore(List<UserInterestData> interests, out InterestsDetails details)
        {
            double score = 5.0; // Базовый балл
            details = new InterestsDetails();

            if (interests.Any())
            {
                details.TotalInterests = interests.Count;
                details.InterestNames = interests.Select(i => i.Name).ToList();
                details.UniqueGroups = interests.Select(i => i.Group).Distinct().Count();

                // Оценка по количеству
                if (details.TotalInterests >= 10)
                    score += 3.0;
                else if (details.TotalInterests >= 7)
                    score += 2.0;
                else if (details.TotalInterests >= 4)
                    score += 1.0;
                else if (details.TotalInterests >= 2)
                    score += 0.5;
                else
                    score -= 0.5;

                // Оценка разнообразия
                if (details.UniqueGroups >= 5)
                    score += 2.0;
                else if (details.UniqueGroups >= 3)
                    score += 1.0;

                // Бонус за определенные группы интересов
                var groupBonuses = new Dictionary<int, double>
                {
                    { 7, 0.5 }, // Valuation
                    { 3, 0.3 }, // Finance
                    { 5, 0.2 }  // Social
                };

                foreach (var interest in interests)
                {
                    if (groupBonuses.TryGetValue(interest.Group, out var bonus))
                        score += bonus;
                }
            }
            else
            {
                score -= 1.0;
            }

            details.Score = Math.Max(0, Math.Min(10, score));
            return details.Score;
        }

        // Вспомогательные классы
        private class MessageStats
        {
            public int TotalMessages { get; set; }
            public DateTime? LastMessageDate { get; set; }
            public double? AverageResponseTime { get; set; }
            public double? ResponseRate { get; set; }
        }

        private class ChatStats
        {
            public int TotalChats { get; set; }
            public int ActiveChats { get; set; }
            public double AverageMessagesPerChat { get; set; }
        }

        private class UserInterestData
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Group { get; set; }
        }
    }
}