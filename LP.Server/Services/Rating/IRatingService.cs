using LP.Server.DTO;

namespace LP.Server.Services.Rating
{
    public interface IRatingService
    {
        Task<RatingDto> CalculateUserRating(Guid userId);
        Task<double> GetCachedRating(Guid userId);
        Task InvalidateCache(Guid userId);
    }
}