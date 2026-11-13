namespace LP.Journal.Services.Journal;

/// <summary>
/// Периодически проверяет включённые источники и создаёт черновики.
/// Интервал проверки задаётся Journal:CrawlCheckIntervalMinutes.
/// </summary>
public sealed class JournalCrawlBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JournalCrawlBackgroundService> _logger;

    public JournalCrawlBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<JournalCrawlBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Journal:CrawlEnabled", true))
        {
            _logger.LogInformation("Journal crawler is disabled by configuration");
            return;
        }

        var checkInterval = TimeSpan.FromMinutes(
            Math.Max(1, _configuration.GetValue("Journal:CrawlCheckIntervalMinutes", 15)));

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
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
                var crawler = scope.ServiceProvider.GetRequiredService<JournalCrawlService>();
                var processed = await crawler.SyncDueSourcesAsync(stoppingToken);

                if (processed > 0)
                {
                    _logger.LogInformation("Journal crawler checked {Count} due sources", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Journal crawler iteration failed");
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
