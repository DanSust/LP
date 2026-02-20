using LP.Chat;
using LP.Chat.Interfaces;
using LP.Entity;
//using LP.Entity.Migrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

public class ChatHub : Hub
{
    private readonly IMessageBuffer _messageBuffer;
    private readonly IMessageCache _messageCache;
    private readonly ILogger<ChatHub> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceProvider _services;
    private readonly IUserPresenceService _presenceService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ChatHub> _hubContext;

    // ✅ ОТСЛЕЖИВАНИЕ ПОДКЛЮЧЕНИЙ
    private static readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();
    public record ConnectionInfo(Guid UserId, Guid ChatId, DateTime ConnectedAt, string ConnectionId);

    public ChatHub(IMessageBuffer messageBuffer, IMessageCache messageCache,
        ILogger<ChatHub> logger, IConnectionMultiplexer redis, 
        IServiceProvider services,
        IServiceScopeFactory scopeFactory,
        IHubContext<ChatHub> hubContext,
        IUserPresenceService presenceService)
    {
        _messageBuffer = messageBuffer;
        _messageCache = messageCache;
        _logger = logger;
        _redis = redis;
        _services = services;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _presenceService = presenceService;
    }

    // ✅ ОТПРАВКА УВЕДОМЛЕНИЙ ОБ ИЗМЕНЕНИИ СТАТУСА
    private async Task NotifyContactsAboutStatusChange(Guid userId, bool isOnline)
    {
        try
        {
            // Не уведомляем, если пользователь всё ещё онлайн (в других вкладках)
            if (!isOnline && _presenceService.IsUserOnline(userId))
                return;

            //await _hubContext.Clients.Clients().SendAsync("userStatusChanged", new
            //{
            //    UserId = userId,
            //    Status = isOnline
            //});

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            // Получаем все чаты пользователя и их участников одним запросом
            var companionChats = await db.Chats
                .Where(cu => cu.UserId == userId || cu.Owner == userId)
                .Select(c => c.UserId == userId ? c.Owner : c.UserId)
                .Distinct()
                .ToListAsync();

            if (!companionChats.Any()) return;

            //companionChats.Add(userId);


            foreach (var chat in companionChats)
            {
                // Отправляем только онлайн участникам
                //if (!_presenceService.IsUserOnline(chat.UserId)) continue;
                //if (!_presenceService.IsUserOnline(chat.Owner)) continue;

                // Находим активные соединения собеседника в этом чате
                var targetConnectionIds = _connections
                        .Where(kvp => kvp.Value.UserId == chat /*|| kvp.Value.UserId == userId*/)
                        .Select(kvp => kvp.Key)
                        .ToList();

                if (!targetConnectionIds.Any()) continue;
                
                await _hubContext.Clients.Clients(targetConnectionIds).SendAsync("userStatusChanged", new
                {
                    UserId = userId,
                    Status = isOnline
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify contacts about status change for user {UserId}", userId);
        }
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} connected", userId);

        _connections.TryAdd(Context.ConnectionId, new ConnectionInfo(
            userId, Guid.Empty, DateTime.UtcNow, Context.ConnectionId
        ));
        _presenceService.UserConnected(userId, Context.ConnectionId);
        
        await NotifyContactsAboutStatusChange(userId, true);
        await base.OnConnectedAsync();
    }

    public async Task StartBotDialog(Guid botUserId)
    {
        var userId = GetUserId();
        var botService = _services.GetRequiredService<IMessageBotService>();

        // botUserId задает вопросы, userId отвечает
        await botService.StartDialogAsync(botUserId, userId);
    }

    public async Task StopBotDialog(Guid botUserId)
    {
        var userId = GetUserId();
        var botService = _services.GetRequiredService<IMessageBotService>();

        // botUserId задает вопросы, userId отвечает
        await botService.StopDialogAsync(botUserId, userId);
    }

    public void StopChat(Guid owner, Guid userId)
    {
        var botService = _services.GetRequiredService<IMessageBotService>();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        
        botService.StopChat(owner, userId);
    }

    public async Task JoinChat(Guid chatId)
    {
        var userId = GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat:{chatId}");

        if (_connections.TryGetValue(Context.ConnectionId, out var existingInfo))
        {
            _connections.TryUpdate(Context.ConnectionId,
                existingInfo with { ChatId = chatId },
                existingInfo);
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var history = await db.Messages
            .Where(m => m.ChatId == chatId)
            .OrderBy(m => m.Time)
            .ToListAsync();

        var tasks = history.Select(item => Clients.Caller.SendAsync("ReceiveMessage", new
        {
            Id = item.Id,
            ChatId = item.ChatId,
            Text = item.Text,
            Own = item.UserId == userId,
            Time = item.Time,
            UserId = item.UserId,
            Status = item.Status ?? "delivered",
            MessageType = item.MessageType // ✅ Передаем тип
        }));

        await Task.WhenAll(tasks);
    }

    public async Task LeaveChat(Guid chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat:{chatId}");
        Console.WriteLine($"✅ User {GetUserId()} left chat {chatId}");
    }

    public async Task SendMessage(Guid chatId, string clientMessageId, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 5000)
            throw new HubException("Invalid message");

        var userId = GetUserId();
        var messageId = Guid.Parse(clientMessageId);

        var message = new Message
        {
            Id = messageId,
            ChatId = chatId,
            UserId = userId,
            Text = text,
            Time = DateTime.UtcNow,
            Status = "delivered",
            MessageType = 0 // Обычное сообщение по умолчанию
        };

        // Проверяем, активен ли диалог с ботом
        var botService = _services.GetRequiredService<IMessageBotService>();
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var chat = await db.Chats.FindAsync(chatId);
        if (chat != null)
        {
            var otherUserId = (chat.UserId == userId) ? chat.Owner : chat.UserId;

            // Если это ответ в активном диалоге с ботом
            if (await botService.IsBotDialogActive(otherUserId, userId))
            {
                message.MessageType = 3; // Ответ на бота
            }
        }

        await _messageBuffer.AddMessageAsync(message);
        await _messageCache.AddToCacheAsync(message);

        // Отправка всем в чате
        var broadcastMessage = new
        {
            Id = messageId,
            ChatId = chatId,
            Text = text,
            Own = false,
            Time = message.Time,
            UserId = userId,
            Status = "delivered",
            MessageType = message.MessageType
        };

        await Clients.OthersInGroup($"chat:{chatId}")
            .SendAsync("ReceiveMessage", broadcastMessage);

        // Подтверждение отправителю
        await Clients.Caller.SendAsync("MessageStatus", new
        {
            ClientMessageId = clientMessageId,
            ServerMessageId = messageId,
            Status = "delivered"
        });

        // Если это ответ боту — обрабатываем
        if (message.MessageType == 3 && chat != null)
        {
            var otherUserId = (chat.UserId == userId) ? chat.Owner : chat.UserId;
            _ = Task.Run(async () =>
            {
                await Task.Delay(200); // Небольшая задержка для надежности
                await botService.ProcessUserResponse(otherUserId, userId, text, chatId);
            });
        }
    }
    internal static IReadOnlyList<ConnectionInfo> GetActiveConnections() => _connections.Values.ToList();
    public async Task MarkAsRead(Guid chatId, Guid messageId)
    {
        var currentUserId = GetUserId();
        var scopeFactory = _scopeFactory;

        // Обновляем в БД (асинхронно, не блокируем)
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            await db.Messages
                .Where(m => m.Id == messageId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "read"));
        });

        // Проверяем онлайн-статус и отправляем уведомление (асинхронно)
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                
                var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

                // ✅ ПРАВИЛЬНЫЙ запрос: получаем ID второго участника чата
                var otherUserId = await db.Chats
                    .Where(cu => cu.Id == chatId )
                    .Select(cu => cu.UserId)
                    .FirstOrDefaultAsync();

                // Если второй участник не найден или он НЕ онлайн - выходим
                if (otherUserId == default || !_presenceService.IsUserOnline(otherUserId))
                    return;

                // Находим connection IDs второго участника в ТЕКУЩЕМ чате
                var targetConnectionIds = _connections
                    .Where(kvp =>
                        kvp.Value.ChatId == chatId )
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (!targetConnectionIds.Any()) return;

                // Публикуем в Redis
                var pubSub = _redis.GetSubscriber();
                await pubSub.PublishAsync($"chat:{chatId}:read", messageId.ToString());

                // Отправляем уведомление только онлайн участникам
                await _hubContext.Clients.Clients(targetConnectionIds).SendAsync("StatusUpdate", new
                {
                    //Type = "status",
                    MessageId = messageId,
                    Status = "read"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send read status notification for message {MessageId}", messageId);
            }
        });
    }

    private Guid GetUserId()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.GetHttpContext()?.Request.Query["userId"];

        if (userId != null && Guid.TryParse(userId, out var result))
            return result;

        throw new HubException("UserId не предоставлен");
    }

    // Метод для получения статуса конкретного пользователя
    public Task<bool> GetUserStatus(Guid userId)
    {
        var serv = _presenceService.IsUserOnline(userId);
        return Task.FromResult(serv);
    }
    public Task<IReadOnlyList<Guid>> GetOnlineUsers()
    {
        var onlineUsers = _presenceService.GetOnlineUsers();
        return Task.FromResult(onlineUsers);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} disconnected", userId);

        _connections.TryRemove(Context.ConnectionId, out var info);
        if (info != null)
            _presenceService.UserDisconnected(info.UserId, Context.ConnectionId);

        await NotifyContactsAboutStatusChange(userId, false);

        // Останавливаем все диалоги с участием пользователя
        var botService = _services.GetRequiredService<IMessageBotService>();

        // Получаем копию всех активных ключей
        var activeKeys = MessageBotService.GetAllActiveDialogKeys().ToList();

        foreach (var key in activeKeys)
        {
            var parts = key.Split(':');
            if (parts.Length == 2 && Guid.TryParse(parts[0], out var fromId) && Guid.TryParse(parts[1], out var toId))
            {
                if (fromId == userId || toId == userId)
                {
                    await botService.StopDialogAsync(fromId, toId);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}