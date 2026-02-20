using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Common.Interfaces
{
    public interface ILikeRestrictionService
    {
        /// <summary>
        /// Проверяет, есть ли взаимный лайк между двумя пользователями
        /// </summary>
        Task<bool> HasMutualLikeAsync(Guid user1, Guid user2);

        /// <summary>
        /// Проверяет, может ли отправитель писать получателю
        /// </summary>
        Task<bool> CanSendMessageAsync(Guid senderId, Guid recipientId);

        /// <summary>
        /// Получает полный статус лайков между пользователями
        /// </summary>
        Task<LikeStatus> GetLikeStatusAsync(Guid currentUserId, Guid otherUserId);
    }

    public class LikeStatus
    {
        public bool HasMutualLike { get; set; }
        public bool ILiked { get; set; }
        public bool TheyLiked { get; set; }
        public bool RequiresMutualLike { get; set; }
        public bool CanChat { get; set; }
    }
}
