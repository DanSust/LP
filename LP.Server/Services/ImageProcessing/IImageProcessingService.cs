// ImageProcessingService.Core/Interfaces/IImageProcessingService.cs
using Microsoft.AspNetCore.Http;

namespace LP.Server.Services.ImageProcessing;

public interface IImageProcessingService
{
    /// <summary>
    /// Создание квадратной иконки из изображения
    /// </summary>
    Task<byte[]> CreateSquareIconAsync(
        byte[] imageData,
        int size = 42,
        ImageProcessingOptions options = null);

    /// <summary>
    /// Создание квадратной иконки из файла IFormFile
    /// </summary>
    Task<byte[]> CreateSquareIconFromFileAsync(
        IFormFile file,
        int size = 42,
        ImageProcessingOptions options = null);

    /// <summary>
    /// Создание квадратной иконки и сохранение в файл
    /// </summary>
    Task<string> CreateSquareIconAndSaveAsync(
        byte[] imageData,
        string outputPath,
        int size = 42,
        ImageProcessingOptions options = null);

    /// <summary>
    /// Общая обработка изображения с произвольными параметрами
    /// </summary>
    Task<ProcessingResult> ProcessImageAsync(
        byte[] imageData,
        ProcessingParameters parameters);
}