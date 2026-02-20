using LP.Common.Interfaces;
using LP.Entity;
using Microsoft.EntityFrameworkCore;

namespace LP.Server.Services
{
    public class LikeRestrictionService : ILikeRestrictionService
    {
        private readonly ApplicationContext _context;

        public LikeRestrictionService(ApplicationContext context)
        {
            _context = context;
        }

        public async Task<bool> HasMutualLikeAsync(Guid user1, Guid user2)
        {
            var user1LikedUser2 = await _context.Votes
                .AsNoTracking()
                .AnyAsync(v =>
                    v.Owner == user1 &&
                    v.Like == user2 &&
                    v.IsLike &&
                    !v.IsReject);

            var user2LikedUser1 = await _context.Votes
                .AsNoTracking()
                .AnyAsync(v =>
                    v.Owner == user2 &&
                    v.Like == user1 &&
                    v.IsLike &&
                    !v.IsReject);

            return user1LikedUser2 && user2LikedUser1;
        }

        public async Task<bool> CanSendMessageAsync(Guid senderId, Guid recipientId)
        {
            var recipientProfile = await _context.Profiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == recipientId);
            
            if (recipientProfile.WithLikes)
                return true;

            // Иначе нужен взаимный лайк
            return await HasMutualLikeAsync(senderId, recipientId);
        }

        public async Task<LikeStatus> GetLikeStatusAsync(Guid currentUserId, Guid otherUserId)
        {
            var iLiked = await _context.Votes
                .AsNoTracking()
                .AnyAsync(v =>
                    v.Owner == currentUserId &&
                    v.Like == otherUserId &&
                    v.IsLike &&
                    !v.IsReject);

            var theyLiked = await _context.Votes
                .AsNoTracking()
                .AnyAsync(v =>
                    v.Owner == otherUserId &&
                    v.Like == currentUserId &&
                    v.IsLike &&
                    !v.IsReject);

            var recipientProfile = await _context.Profiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == otherUserId);

            var requiresMutualLike = recipientProfile?.WithLikes ?? false;

            return new LikeStatus
            {
                HasMutualLike = iLiked && theyLiked,
                ILiked = iLiked,
                TheyLiked = theyLiked,
                RequiresMutualLike = requiresMutualLike,
                CanChat = !requiresMutualLike || (iLiked && theyLiked)
            };
        }
    }
}
