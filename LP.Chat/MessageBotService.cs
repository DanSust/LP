// LP.Chat/MessageBotService.cs
using LP.Chat.Interfaces;
using LP.Entity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace LP.Chat;

public class MessageBotService : IMessageBotService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IMessageBuffer _messageBuffer;
    private readonly IUserPresenceService _presenceService;
    private readonly ILogger<MessageBotService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    // Храним прогресс: "fromUserId:toUserId" -> последний Order
    private static readonly ConcurrentDictionary<string, int> _activeDialogs = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _dialogLocks = new();

    public MessageBotService(
        IHubContext<ChatHub> hubContext,
        IMessageBuffer messageBuffer,
        IUserPresenceService presenceService,
        ILogger<MessageBotService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _hubContext = hubContext;
        _messageBuffer = messageBuffer;
        _presenceService = presenceService;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    private static string GetDialogKey(Guid fromUserId, Guid toUserId) => $"{fromUserId}:{toUserId}";
    public static IEnumerable<string> GetAllActiveDialogKeys()
    {
        return _activeDialogs.Keys.ToList();
    }

    public async Task StartDialogAsync(Guid fromUserId, Guid toUserId)
    {
        var key = GetDialogKey(fromUserId, toUserId);

        if (_activeDialogs.ContainsKey(key))
        {
            _logger.LogWarning("Bot dialog already active: {Key}", key);
            return;
        }
        using var scope = _scopeFactory.CreateScope();
        var _db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        // Проверяем, есть ли вопросы для toUserId
        var hasQuestions = await _db.UserQuestions
            .AnyAsync(q => q.User.Id == fromUserId);

        if (!hasQuestions)
        {
            _logger.LogWarning("No active questions for user {ToUserId}", toUserId);
            return;
        }

        _activeDialogs.TryAdd(key, -1);
        _logger.LogInformation("Bot dialog started: {Key}", key);

        await SendNextQuestionAsync(fromUserId, toUserId);
    }

    private async Task SendNextQuestionAsync(Guid fromUserId, Guid toUserId)
    {
        if (!_presenceService.IsUserOnline(toUserId))
        {
            _logger.LogInformation("User {ToUserId} offline, pausing bot for {Key}",
                toUserId, GetDialogKey(fromUserId, toUserId));
            return;
        }

        var key = GetDialogKey(fromUserId, toUserId);
        var lastOrder = _activeDialogs[key];

        using var scope = _scopeFactory.CreateScope();
        var _db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var nextQuestion = await _db.UserQuestions
            .Where(q => q.User.Id == fromUserId && q.Order > lastOrder)
            .OrderBy(q => q.Order)
            .FirstOrDefaultAsync();

        if (nextQuestion == null)
        {
            await EndDialogAsync(fromUserId, toUserId, "✅ Все вопросы завершены!");
            return;
        }

        var chatId = await GetOrCreateChatAsync(toUserId, fromUserId);
        var messageId = Guid.NewGuid();

        var message = new Message
        {
            Id = messageId,
            ChatId = chatId,
            UserId = fromUserId,
            Text = nextQuestion.Question,
            Time = DateTime.UtcNow,
            Status = "delivered",
            MessageType = 2 // Вопрос бота
        };

        await _messageBuffer.AddMessageAsync(message);

        var connectionIds = ChatHub.GetActiveConnections()
            .Where(c => c.UserId == toUserId)
            .Select(c => c.ConnectionId)
            .ToList();

        if (connectionIds.Any())
        {
            await _hubContext.Clients.Clients(connectionIds).SendAsync("ReceiveMessage", new
            {
                Id = messageId,
                ChatId = chatId,
                Text = nextQuestion.Question,
                Own = false,
                Time = message.Time,
                UserId = fromUserId,
                Status = "delivered",
                MessageType = 2
            });

            _activeDialogs[key] = nextQuestion.Order;
            _logger.LogInformation("Bot sent question #{Order} for {Key}", nextQuestion.Order, key);
        }
    }

    public async Task ProcessUserResponse(Guid fromUserId, Guid toUserId, string responseText, Guid chatId)
    {
        var key = GetDialogKey(fromUserId, toUserId);
        if (!_activeDialogs.ContainsKey(key))
        {
            _logger.LogWarning("No active dialog: {Key}", key);
            return;
        }

        var lockObj = _dialogLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await lockObj.WaitAsync();
        try
        {
            // Повторная проверка после получения блокировки
            if (!_activeDialogs.ContainsKey(key))
            {
                _logger.LogWarning("Dialog ended while waiting for lock: {Key}", key);
                return;
            }

            _logger.LogInformation("Processing response for {Key}: {Text}", key, responseText);

            // Сохраняем ответ как сообщение типа 3
            var responseMessageId = Guid.NewGuid();
            var responseMessage = new Message
            {
                Id = responseMessageId,
                ChatId = chatId,
                UserId = toUserId,
                Text = responseText,
                Time = DateTime.UtcNow,
                Status = "delivered",
                MessageType = 3
            };

            // Отправляем следующий вопрос
            await SendNextQuestionAsync(fromUserId, toUserId);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public Task<bool> IsBotDialogActive(Guid fromUserId, Guid toUserId)
    {
        var key = GetDialogKey(fromUserId, toUserId);
        return Task.FromResult(_activeDialogs.ContainsKey(key));
    }

    public async Task StopDialogAsync(Guid fromUserId, Guid toUserId)
    {
        var key = GetDialogKey(fromUserId, toUserId);
        if (_activeDialogs.TryRemove(key, out _))
        {
            if (_dialogLocks.TryRemove(key, out var lockObj))
            {
                lockObj.Dispose();
            }
            _logger.LogInformation("Dialog stopped: {Key}", key);

            var connections = ChatHub.GetActiveConnections()
                .Where(c => c.UserId == toUserId)
                .Select(c => c.ConnectionId)
                .ToList();

            if (connections.Any())
            {
                await _hubContext.Clients.Clients(connections)
                    .SendAsync("BotDialogEnded", new { FromUserId = fromUserId, ToUserId = toUserId });
            }
        }
    }

    public void StopChat(Guid fromUserId, Guid toUserId)
    {
        var key = GetDialogKey(fromUserId, toUserId);
        if (_activeDialogs.TryRemove(key, out _))
        {
            if (_dialogLocks.TryRemove(key, out var lockObj))
            {
                lockObj.Dispose();
            }
        }
    }

    private async Task EndDialogAsync(Guid fromUserId, Guid toUserId, string finalMessage)
    {
        var key = GetDialogKey(fromUserId, toUserId);
        _activeDialogs.TryRemove(key, out _);

        var chatId = await GetOrCreateChatAsync(toUserId, fromUserId);
        var messageId = Guid.NewGuid();

        var message = new Message
        {
            Id = messageId,
            ChatId = chatId,
            UserId = fromUserId,
            Text = finalMessage,
            Time = DateTime.UtcNow,
            Status = "delivered",
            MessageType = 1 // Системное сообщение
        };

        await _messageBuffer.AddMessageAsync(message);
        _logger.LogInformation("Dialog completed: {Key}", key);

        var connections = ChatHub.GetActiveConnections()
            .Where(c => c.UserId == toUserId)
            .Select(c => c.ConnectionId)
            .ToList();

        if (connections.Any())
        {
            await _hubContext.Clients.Clients(connections).SendAsync("ReceiveMessage", new
            {
                Id = messageId,
                ChatId = chatId,
                Text = finalMessage,
                Own = false,
                Time = message.Time,
                UserId = fromUserId,
                Status = "delivered",
                MessageType = 1
            });

            await _hubContext.Clients.Clients(connections)
                .SendAsync("BotDialogEnded", new { FromUserId = fromUserId, ToUserId = toUserId });
        }
    }

    private async Task<Guid> GetOrCreateChatAsync(Guid userId1, Guid userId2)
    {
        using var scope = _scopeFactory.CreateScope();
        var _db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        var chat = await _db.Chats
            .FirstOrDefaultAsync(c =>
                (c.UserId == userId1 && c.Owner == userId2) ||
                (c.UserId == userId2 && c.Owner == userId1));

        if (chat != null) return chat.Id;

        chat = new Entity.Chat
        {
            Id = Guid.NewGuid(),
            UserId = userId1,
            Owner = userId2
        };

        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        return chat.Id;
    }
}