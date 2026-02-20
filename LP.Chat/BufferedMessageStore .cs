using LP.Chat.Interfaces;
using LP.Entity;
using System.Collections.Concurrent;
using System.Threading.Channels;

public class BufferedMessageStore : IMessageBuffer, IDisposable
{
    private readonly Channel<Message> _channel;
    private readonly IServiceProvider _services;
    private readonly ILogger<BufferedMessageStore> _logger;
    private readonly CancellationTokenSource _shutdownCts = new();
    private const int BatchSize = 200;

    public BufferedMessageStore(IServiceProvider services, ILogger<BufferedMessageStore> logger)
    {
        _services = services;
        _logger = logger;

        var channelOptions = new BoundedChannelOptions(BatchSize * 10)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        };

        _channel = Channel.CreateBounded<Message>(channelOptions);
        StartProcessing();
    }

    public async Task AddMessageAsync(Message message)
    {
        await _channel.Writer.WriteAsync(message, _shutdownCts.Token);
    }

    private void StartProcessing()
    {
        Task.Run(async () =>
        {
            //var batch = new List<Message>(BatchSize);

            try
            {
                // Читаем пока не отменили
                await foreach (var message in _channel.Reader.ReadAllAsync(_shutdownCts.Token))
                {
                    await SaveMessageAsync(message);
                    //batch.Add(message);

                    //if (batch.Count >= BatchSize)
                    //{
                    //    await SaveBatchAsync(batch);
                    //batch.Clear();
                    //}
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Message processing loop cancelled gracefully");
            }

            // Сохраняем остатки
            //if (batch.Count > 0)
            //{
            //    await SaveBatchAsync(batch);
            //}
        }, _shutdownCts.Token);
    }

    private async Task SaveMessageAsync(Message message)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            await db.Messages.AddAsync(message);
            await db.SaveChangesAsync();

            _logger.LogInformation("Saved message to DB");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save message");
        }
    }

    private async Task SaveBatchAsync(List<Message> messages)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            await db.Messages.AddRangeAsync(messages);
            await db.SaveChangesAsync();

            _logger.LogInformation("Saved {Count} messages to DB", messages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save message batch");
        }
    }

    // ✅ ГРАЦИОЗНОЕ Завершение (вызывается при shutdown)
    public async Task ForceFlushAsync()
    {
        _logger.LogInformation("Shutting down message buffer...");

        // Сигнализируем об отмене
        _shutdownCts.CancelAsync();

        // Ждем завершения обработки (до 5 сек)
        await Task.WhenAny(
            _channel.Reader.Completion,
            Task.Delay(5000)
        );

        _logger.LogInformation("Message buffer shutdown complete");
    }

    public void Dispose()
    {
        _shutdownCts?.Cancel();
        _shutdownCts?.Dispose();
    }
}