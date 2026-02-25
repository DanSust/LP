// ImageProcessingService.Core/DependencyInjection.cs

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LP.Server.Services.ImageProcessing;

public static class DependencyInjection
{
    public static IServiceCollection AddImageProcessingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Регистрация конфигурации
        services.Configure<ServiceConfiguration>(
            configuration.GetSection("ImageProcessing"));

        // Регистрация сервиса как Singleton (можно Scoped или Transient)
        services.AddSingleton<IImageProcessingService, ImageProcessingService>();

        // Добавляем кэширование если нужно
        //services.AddMemoryCache();

        return services;
    }
}