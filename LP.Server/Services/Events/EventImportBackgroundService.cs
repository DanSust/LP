namespace LP.Server.Services.Events;

/// <summary>
/// Периодически проверяет включённые источники и импортирует новые события в черновики.
/// </summary>
public sealed class EventImportBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EventImportBackgroundService> _logger;

    public EventImportBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<EventImportBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Events:ImportEnabled", true))
        {
            _logger.LogInformation("Event import is disabled by configuration");
            return;
        }

        var checkInterval = TimeSpan.FromMinutes(
            Math.Max(1, _configuration.GetValue("Events:ImportCheckIntervalMinutes", 15)));

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var importer = scope.ServiceProvider.GetRequiredService<EventImportService>();
                var processed = await importer.SyncDueSourcesAsync(stoppingToken);

                if (processed > 0)
                {
                    _logger.LogInformation("Event import checked {Count} due sources", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event import background iteration failed");
            }

            try
            {
                await Task.Delay(checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
