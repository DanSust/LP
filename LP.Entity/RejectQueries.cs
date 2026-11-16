using Microsoft.EntityFrameworkCore;

namespace LP.Entity
{
    /// <summary>
    /// Общие запросы по жалобам/блокировкам (<see cref="Reject"/>).
    /// Используются и в LP.Server, и в LP.Chat, поэтому живут в LP.Entity.
    /// </summary>
    public static class RejectQueries
    {
        /// <summary>
        /// Id пользователей, между которыми есть блокировка в любую сторону.
        /// </summary>
        public static IQueryable<Guid> BlockedUserIds(this ApplicationContext db, Guid userId) =>
            db.Rejects
                .AsNoTracking()
                .Where(r => r.Reason == Reason.rsBlock &&
                            (r.Owner == userId || r.UserId == userId))
                .Select(r => r.Owner == userId ? r.UserId : r.Owner)
                .Distinct();

        /// <summary>
        /// Id пользователей, которых текущий пользователь не должен видеть
        /// в лентах: заблокированные им, заблокировавшие его и те, на кого
        /// он пожаловался как на спам.
        /// </summary>
        public static IQueryable<Guid> HiddenUserIds(this ApplicationContext db, Guid userId) =>
            db.Rejects
                .AsNoTracking()
                .Where(r => (r.Owner == userId && (r.Reason == Reason.rsBlock || r.Reason == Reason.rsSpam)) ||
                            (r.UserId == userId && r.Reason == Reason.rsBlock))
                .Select(r => r.Owner == userId ? r.UserId : r.Owner)
                .Distinct();

        /// <summary>
        /// Есть ли блокировка между пользователями в любую сторону.
        /// </summary>
        public static Task<bool> IsBlockedEitherWayAsync(
            this ApplicationContext db,
            Guid firstUserId,
            Guid secondUserId,
            CancellationToken cancellationToken = default) =>
            db.Rejects
                .AsNoTracking()
                .AnyAsync(r => r.Reason == Reason.rsBlock &&
                               ((r.Owner == firstUserId && r.UserId == secondUserId) ||
                                (r.Owner == secondUserId && r.UserId == firstUserId)),
                          cancellationToken);
    }
}
