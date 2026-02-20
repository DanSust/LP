// LP.Entity/Store/IUserStore.cs
using LP.Common;

namespace LP.Entity.Interfaces;

public interface IUserStore
{
    /// <summary>
    /// Получить или создать пользователя по данным от OAuth провайдера
    /// </summary>
    Task<User> GetOrCreateAsync(UserClaims claims, string provider);

    /// <summary>
    /// Найти пользователя по ID
    /// </summary>
    Task<User?> GetByIdAsync(Guid id);

    /// <summary>
    /// Найти пользователя по провайдеру и внешнему ID
    /// </summary>
    Task<User?> GetByProviderAsync(string provider, string providerId);

    /// <summary>
    /// Найти пользователя по email
    /// </summary>
    Task<User?> GetByEmailAsync(string email);

    /// <summary>
    /// Обновить время последнего входа
    /// </summary>
    Task UpdateLastLoginAsync(Guid userId);

    /// <summary>
    /// Проверить существование пользователя с таким email
    /// </summary>
    Task<bool> ExistsByEmailAsync(string email);
}