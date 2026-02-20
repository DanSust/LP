namespace LP.Common;

public record UserClaims(
    string ProviderId,      // Уникальный ID от провайдера (sub, id, и т.д.)
    string? Email,           // Email (может быть null для Telegram)
    string? Username,        // Username/screen_name
    string? FirstName,       // Имя
    string? LastName,        // Фамилия
    string? FullName,        // Полное имя (если есть отдельно)
    string? AvatarUrl        // URL аватара
);