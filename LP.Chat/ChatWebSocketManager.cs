using LP.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LP.Chat
{
    // DTO для клиента
    public record WebSocketMessage(Guid Id, Guid chatId, string Text, bool Own, DateTime Time, string Status, Guid? UserId = null);
    public record StatusUpdateRequest(
        [property: JsonPropertyName("type")]
        string Type, // "status"
        [property: JsonPropertyName("messageId")]
        string MessageId,
        [property: JsonPropertyName("status")]
        string Status // "read"
    );
    public record SimpleMessage(
        [property: JsonPropertyName("id")]
        string Id,
        [property: JsonPropertyName("text")]
        string Text,
        [property: JsonPropertyName("time")]
        DateTime Time
    );


    // WebSocket Manager
    public class ChatWebSocketManager(IServiceProvider services)
    {
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, List<WebSocket>>> _connections = new();
        private readonly IServiceProvider _services = services;
        private Guid _userId;

        public async Task HandleWebSocket(Guid chatId, Guid userId, WebSocket webSocket)
        {
            _userId = userId;
            var chatConnections = _connections.GetOrAdd(chatId, _ => new ConcurrentDictionary<Guid, List<WebSocket>>());
            //chatConnections.TryAdd(userId, webSocket);
            var userSockets = chatConnections.GetOrAdd(userId, _ => new List<WebSocket>());

            lock (userSockets)
            {
                userSockets.Add(webSocket);
            }

            // Send welcome message
            //await SendMessage(webSocket, new WebSocketMessage(
            //    Id: Guid.NewGuid(),
            //    chatId: chatId,
            //    Text: $"Вы подключились к чату {chatId}",
            //    Own: false,
            //    Time: DateTime.UtcNow,
            //    Status: "delivered"
            ////UserId: "System"
            //));

            await LoadHistory(chatId, webSocket);

            var buffer = new byte[1024 * 4];
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);

                        var statusRequest = JsonSerializer.Deserialize<StatusUpdateRequest>(messageJson);
                        // Обработка запроса на изменение статуса
                        if (statusRequest != null && statusRequest.Type == "status")
                        {
                            if (Guid.TryParse(statusRequest.MessageId, out var msgId))
                            {
                                await UpdateMessageStatus(chatId, msgId, statusRequest.Status, userId);
                            }
                            continue;
                        }
                        
                        var incomingMessage = JsonSerializer.Deserialize<SimpleMessage>(messageJson);

                        if (incomingMessage != null)
                        {
                            var clientMessageId = Guid.Parse(incomingMessage.Id);
                            await SaveMessage(chatId, userId, clientMessageId, "''", incomingMessage.Text);
                            await BroadcastMessage(chatId, userId, clientMessageId, incomingMessage.Text);
                            await SendStatusUpdate(chatId, userId, clientMessageId, "delivered");
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"WebSocket error: {ex.Message}"); }
            finally
            {
                lock (userSockets)
                {
                    userSockets.Remove(webSocket);
                    if (userSockets.Count == 0)
                    {
                        chatConnections.TryRemove(userId, out _);
                    }
                }
                if (chatConnections.IsEmpty)
                {
                    _connections.TryRemove(chatId, out _);
                }
            }
        }

        private async Task SendStatusUpdate(Guid chatId, Guid userId, Guid messageId, string status)
        {
            if (_connections.TryGetValue(chatId, out var chatConnections))
            {
                if (chatConnections.TryGetValue(userId, out var sockets))
                {
                    IEnumerable<Task> tasks;
                    lock (sockets)
                    {
                        var statusUpdate = new
                        {
                            type = "status",
                            messageId = messageId.ToString(),
                            status = status
                        };

                        var json = JsonSerializer.Serialize(statusUpdate, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });

                        tasks = sockets
                            .Where(s => s.State == WebSocketState.Open)
                            .Select(socket => socket.SendAsync(
                                new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None
                            ));

                        
                    }
                    await Task.WhenAll(tasks);
                }
            }
        }
        private async Task UpdateMessageStatus(Guid chatId, Guid messageId, string status, Guid updatedByUserId)
        {
            // Обновляем в БД
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
            if (message != null)
            {
                message.Status = status;
                await db.SaveChangesAsync();
            }

            // Отправляем обновление всем участникам чата
            if (_connections.TryGetValue(chatId, out var chatConnections))
            {
                var statusUpdate = new
                {
                    type = "status",
                    messageId = messageId.ToString(),
                    status = status
                };

                var json = JsonSerializer.Serialize(statusUpdate, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var tasks = new List<Task>();
                foreach (var (userId, sockets) in chatConnections)
                {
                    lock (sockets)
                    {
                        tasks.AddRange(sockets
                            .Where(s => s.State == WebSocketState.Open)
                            .Select(socket => socket.SendAsync(
                                new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None
                            )));
                    }
                }

                await Task.WhenAll(tasks);
            }
        }
        private async Task SaveMessage(Guid chatId, Guid userId, Guid messageId, string userName, string text)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var chat = db.Chats.FirstOrDefault(x => x.Id == chatId);
            if (chat == null)
            {
            }

            var entity = new Entity.Message()
            {
                Id = messageId,
                ChatId = chatId,
                UserId = userId,
                //UserName = userName,
                Text = text
            };

            db.Messages.Add(entity);
            await db.SaveChangesAsync();
        }
        private async Task BroadcastMessage(Guid chatId, Guid senderId, Guid messageId, string text)
        {
            if (_connections.TryGetValue(chatId, out var chatConnections))
            {
                var message = new WebSocketMessage(
                    chatId: chatId,
                    Id: messageId,
                    Text: text,
                    Own: false,
                    Time: DateTime.UtcNow,
                    Status: "delivered",
                    UserId: senderId
                //UserName: $"User_{senderId.Substring(Math.Max(0, senderId.Length - 4))}"
                );

                var tasks = new List<Task>();
                foreach (var (userId, sockets) in chatConnections)
                {
                    lock (sockets)
                    {
                        foreach (var socket in sockets)
                        {
                            if (userId == senderId) continue;
                            if (socket.State == WebSocketState.Open)
                            {
                                var personalized = message with { Own = userId == senderId };
                                tasks.Add(SendMessage(socket, personalized));
                            }
                        }
                    }
                }

                await Task.WhenAll(tasks);
            }
        }
        private static async Task SendMessage(WebSocket socket, WebSocketMessage message)
        {
            if (socket.State == WebSocketState.Open)
            {
                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await socket.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
        }
        private async Task LoadHistory(Guid chatId, WebSocket socket)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var history = await db.Messages
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.Time)
                .Take(50)
                .OrderBy(m => m.Time)
                .ToListAsync();

            foreach (var item in history)
            {
                var msg = new WebSocketMessage(
                    chatId: item.ChatId,
                    Id: item.Id,
                    Text: item.Text,
                    Own: false, //_userId == item.UserId,
                    Time: item.Time,
                    UserId: item.UserId,
                    Status: item.Status ?? "delivered"
                );
                await SendMessage(socket, msg);
            }
        }
    }
}
