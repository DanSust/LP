using LP.Chat.Interfaces;

public static class GracefulShutdown
{
    public static void Configure(WebApplication app)
    {
        var lifetime = app.Lifetime;
        var logger = app.Services.GetRequiredService<ILogger<ChatHub>>();

        lifetime.ApplicationStopping.Register(async () =>
        {
            logger.LogWarning("Shutting down... Flushing message buffer...");

            var buffer = app.Services.GetRequiredService<IMessageBuffer>();
            await buffer.ForceFlushAsync();

            logger.LogWarning("Shutdown complete");
        });
    }
}