// LP.Entity/Store/UserStore.cs
using LP.Common;
using LP.Entity.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace LP.Entity.Store;

public sealed class UserStore : IUserStore
{
    private readonly ApplicationContext _db;
    private readonly ILogger<UserStore> _logger;

    public UserStore(ApplicationContext db, ILogger<UserStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<User> GetOrCreateAsync(UserClaims claims, string provider)
    {
        // 1. Ищем по Provider + ProviderId
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Provider == provider &&
                                      u.ProviderId == claims.ProviderId);

        if (user is not null)
        {
            _logger.LogInformation("Existing user found: {UserId} via {Provider}",
                user.Id, provider);

            // Обновляем данные
            user.LastLogin = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(claims.Email))
                user.Email = claims.Email;

            if (!string.IsNullOrEmpty(claims.FullName))
                user.Caption = claims.FullName;

            await _db.SaveChangesAsync();
            return user;
        }

        // 2. Проверяем email для связывания аккаунтов
        if (!string.IsNullOrEmpty(claims.Email))
        {
            var existingByEmail = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == claims.Email);

            if (existingByEmail is not null)
            {
                _logger.LogInformation("Linking {Provider} to existing user {UserId}",
                    provider, existingByEmail.Id);

                existingByEmail.Provider = provider;
                existingByEmail.ProviderId = claims.ProviderId;
                existingByEmail.LastLogin = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                return existingByEmail;
            }
        }

        // 3. Создаем нового пользователя
        var username = await GenerateUsernameAsync(claims, provider);
        var caption = !string.IsNullOrEmpty(claims.FullName)
            ? claims.FullName
            : $"{claims.FirstName} {claims.LastName}".Trim();

        user = new User
        {
            Id = Guid.NewGuid(),
            Email = claims.Email,
            Username = username,
            Caption = string.IsNullOrEmpty(caption) ? "Пользователь" : caption,
            Provider = provider,
            ProviderId = claims.ProviderId,
            Created = DateTime.UtcNow,
            LastLogin = DateTime.UtcNow
        };

        _db.Users.Add(user);

        // Создаем профиль
        var profile = new Profile
        {
            UserId = user.Id,
            //AvatarUrl = claims.AvatarUrl
        };
        _db.Profiles.Add(profile);

        await _db.SaveChangesAsync();

        _logger.LogInformation("Created new user: {UserId} via {Provider}",
            user.Id, provider);

        return user;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByProviderAsync(string provider, string providerId)
    {
        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Provider == provider &&
                                      u.ProviderId == providerId);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrEmpty(email))
            return null;

        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is not null)
        {
            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        if (string.IsNullOrEmpty(email))
            return false;

        return await _db.Users.AnyAsync(u => u.Email == email);
    }

    #region Helpers

    private async Task<string> GenerateUsernameAsync(UserClaims claims, string provider)
    {
        // Пробуем username от провайдера
        if (!string.IsNullOrEmpty(claims.Username))
        {
            var baseName = claims.Username.ToLowerInvariant();
            if (!await UsernameExistsAsync(baseName))
                return baseName;

            // Добавляем суффикс если занято
            for (int i = 1; i <= 1000; i++)
            {
                var candidate = $"{baseName}{i}";
                if (!await UsernameExistsAsync(candidate))
                    return candidate;
            }
        }

        // На основе имени
        if (!string.IsNullOrEmpty(claims.FirstName))
        {
            var transliterated = Transliterate(
                $"{claims.FirstName}_{claims.LastName}".Trim('_').ToLowerInvariant());

            if (string.IsNullOrEmpty(transliterated))
                transliterated = $"user_{Guid.NewGuid():N}[..8]";

            if (!await UsernameExistsAsync(transliterated))
                return transliterated;

            return $"{transliterated}_{Guid.NewGuid():N}[..4]";
        }

        // Fallback
        return $"{provider}_{claims.ProviderId}";
    }

    private async Task<bool> UsernameExistsAsync(string username)
    {
        return await _db.Users.AsNoTracking().AnyAsync(u => u.Username == username);
    }

    private static string Transliterate(string input)
    {
        var map = new Dictionary<char, string>
        {
            ['а'] = "a",
            ['б'] = "b",
            ['в'] = "v",
            ['г'] = "g",
            ['д'] = "d",
            ['е'] = "e",
            ['ё'] = "yo",
            ['ж'] = "zh",
            ['з'] = "z",
            ['и'] = "i",
            ['й'] = "y",
            ['к'] = "k",
            ['л'] = "l",
            ['м'] = "m",
            ['н'] = "n",
            ['о'] = "o",
            ['п'] = "p",
            ['р'] = "r",
            ['с'] = "s",
            ['т'] = "t",
            ['у'] = "u",
            ['ф'] = "f",
            ['х'] = "h",
            ['ц'] = "ts",
            ['ч'] = "ch",
            ['ш'] = "sh",
            ['щ'] = "sch",
            ['ъ'] = "",
            ['ы'] = "y",
            ['ь'] = "",
            ['э'] = "e",
            ['ю'] = "yu",
            ['я'] = "ya",
            [' '] = "_"
        };

        var sb = new StringBuilder();
        foreach (var c in input.ToLowerInvariant())
        {
            if (map.TryGetValue(c, out var replacement))
                sb.Append(replacement);
            else if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
        }

        return sb.ToString();
    }

    #endregion
}