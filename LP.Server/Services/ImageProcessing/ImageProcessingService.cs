// ImageProcessingService.Core/Services/ImageProcessingService.cs
using LP.Server.Services.ImageProcessing;
using Openize.Heic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Openize.Heic.Decoder;
using SkiaSharp;
using System.Diagnostics;

namespace LP.Server.Services.ImageProcessing;

public class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;
    private readonly ServiceConfiguration _configuration;

    public ImageProcessingService(
        ILogger<ImageProcessingService> logger,
        IOptions<ServiceConfiguration> configuration)
    {
        _logger = logger;
        _configuration = configuration.Value;
    }

    private bool IsHeicFile(byte[] data)
    {
        if (data.Length < 12) return false;

        // HEIC файлы начинаются с сигнатуры "ftyp" + "heic" или "heix" и т.д.
        // Проверяем наличие ftyp и соответствующих кодов
        string header = System.Text.Encoding.ASCII.GetString(data, 4, 8);
        return header.Contains("ftyp") &&
               (header.Contains("heic") || header.Contains("heix") ||
                header.Contains("hevc") || header.Contains("hevx"));
    }

    private SKBitmap ConvertHeicToSkBitmap(byte[] heicData)
    {
        using var ms = new MemoryStream(heicData);

        // Загружаем HEIC изображение через Openize
        var heicImage = HeicImage.Load(ms);

        // Получаем пиксели в формате ARGB32
        int[] pixels = heicImage.GetInt32Array(Openize.Heic.Decoder.PixelFormat.Argb32);
        int width = (int)heicImage.Width;
        int height = (int)heicImage.Height;

        // Создаем SKBitmap и заполняем пикселями
        var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

        // Важно: SKBitmap ожидает BGRA, а у нас ARGB - нужно конвертировать
        unsafe
        {
            byte* ptr = (byte*)bitmap.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                int pixel = pixels[i];
                // ARGB to BGRA
                byte a = (byte)((pixel >> 24) & 0xFF);
                byte r = (byte)((pixel >> 16) & 0xFF);
                byte g = (byte)((pixel >> 8) & 0xFF);
                byte b = (byte)(pixel & 0xFF);

                ptr[i * 4] = b;      // Blue
                ptr[i * 4 + 1] = g;  // Green
                ptr[i * 4 + 2] = r;  // Red
                ptr[i * 4 + 3] = a;  // Alpha
            }
        }

        return bitmap;
    }

    private byte[] ProcessBitmap(SKBitmap bitmap, int size, ImageProcessingOptions options)
    {
        // Здесь ваша существующая логика обработки (Crop, Pad, Stretch)
        // ... (как было в предыдущих примерах)

        // В конце сохраняем
        using var image = SKImage.FromBitmap(bitmap);
        using var data = EncodeImage(image, options.OutputFormat, options.Quality);
        return data.ToArray();
    }

    /// <inheritdoc />
    public async Task<byte[]> CreateSquareIconAsync(
        byte[] imageData,
        int size = 42,
        ImageProcessingOptions options = null)
    {
        //options ??= new ImageProcessingOptions();

        //var stopwatch = Stopwatch.StartNew();

        //try
        //{
        //    using var ms = new MemoryStream(imageData);
        //    using var stream = new SKManagedStream(ms);
        //    using var bitmap = SKBitmap.Decode(stream);

        //    if (bitmap == null)
        //        throw new ArgumentException("Не удалось декодировать изображение");

        //    var result = options.CropMode switch
        //    {
        //        CropMode.Center => CropToSquare(bitmap, size, options),
        //        CropMode.Pad => PadToSquare(bitmap, size, options),
        //        CropMode.Stretch => StretchToSquare(bitmap, size, options),
        //        _ => CropToSquare(bitmap, size, options)
        //    };

        //    _logger.LogInformation(
        //        "Иконка создана за {ElapsedMs}мс, размер: {Size} байт",
        //        stopwatch.ElapsedMilliseconds,
        //        result.Length);

        //    return result;
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogError(ex, "Ошибка при создании иконки");
        //    throw;
        //}
        options ??= new ImageProcessingOptions();

        try
        {
            // Пробуем сначала как обычное изображение (JPEG, PNG)
            using var ms = new MemoryStream(imageData);

            // Проверяем, не HEIC ли это
            if (IsHeicFile(imageData))
            {
                // Конвертируем HEIC в SKBitmap
                using var bitmap = ConvertHeicToSkBitmap(imageData);
                return ProcessBitmap(bitmap, size, options);
            }
            else
            {
                // Обычная обработка через SkiaSharp
                using var stream = new SKManagedStream(ms);
                using var bitmap = SKBitmap.Decode(stream);
                if (bitmap == null)
                    throw new ArgumentException("Не удалось декодировать изображение");

                return ProcessBitmap(bitmap, size, options);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке изображения");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> CreateSquareIconFromFileAsync(
        IFormFile file,
        int size = 42,
        ImageProcessingOptions options = null)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Файл не может быть пустым");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        return await CreateSquareIconAsync(ms.ToArray(), size, options);
    }

    /// <inheritdoc />
    public async Task<string> CreateSquareIconAndSaveAsync(
        byte[] imageData,
        string outputPath,
        int size = 42,
        ImageProcessingOptions options = null)
    {
        var result = await CreateSquareIconAsync(imageData, size, options);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(outputPath, result);
        return outputPath;
    }

    /// <inheritdoc />
    public async Task<ProcessingResult> ProcessImageAsync(
        byte[] imageData,
        ProcessingParameters parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ProcessingResult();

        try
        {
            using var ms = new MemoryStream(imageData);
            using var stream = new SKManagedStream(ms);
            using var bitmap = SKBitmap.Decode(stream);

            if (bitmap == null)
            {
                result.Success = false;
                result.ErrorMessage = "Не удалось декодировать изображение";
                return result;
            }

            var processedBitmap = bitmap;

            // Изменение размера если нужно
            if (parameters.Width.HasValue || parameters.Height.HasValue)
            {
                processedBitmap = ResizeImage(
                    bitmap,
                    parameters.Width,
                    parameters.Height,
                    parameters.MaintainAspectRatio);
            }

            // Обрезка до квадрата если нужно
            if (parameters.CropToSquare)
            {
                processedBitmap = CropToSquareBitmap(
                    processedBitmap ?? bitmap,
                    parameters.SquareCropMode);
            }

            // Сохранение в нужном формате
            using var image = SKImage.FromBitmap(processedBitmap);
            using var encoded = EncodeImage(image, parameters.OutputFormat, parameters.Quality);

            result.Data = encoded.ToArray();
            result.ContentType = GetContentType(parameters.OutputFormat);
            result.Width = processedBitmap.Width;
            result.Height = processedBitmap.Height;
            result.SizeInBytes = result.Data.Length;
            result.ProcessingTime = stopwatch.Elapsed;
            result.Success = true;

            if (processedBitmap != bitmap)
                processedBitmap.Dispose();

            _logger.LogInformation(
                "Изображение обработано за {ElapsedMs}мс",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Ошибка при обработке изображения");
        }

        return result;
    }

    #region Private Methods

    private byte[] CropToSquare(SKBitmap original, int size, ImageProcessingOptions options)
    {
        int cropSize = Math.Min(original.Width, original.Height);
        int x = (original.Width - cropSize) / 2;
        int y = (original.Height - cropSize) / 2;

        var cropRect = new SKRectI(x, y, x + cropSize, y + cropSize);

        using var cropped = new SKBitmap(cropSize, cropSize);
        original.ExtractSubset(cropped, cropRect);

        using var resized = cropped.Resize(
            new SKImageInfo(size, size),
            SKFilterQuality.High);

        using var image = SKImage.FromBitmap(resized);
        using var data = EncodeImage(image, options.OutputFormat, options.Quality);

        return data.ToArray();
    }

    private byte[] PadToSquare(SKBitmap original, int size, ImageProcessingOptions options)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var canvas = surface.Canvas;

        // Очистка фона
        if (options.OutputFormat == OutputFormat.Png && options.BackgroundColor == "#FFFFFF")
        {
            canvas.Clear(SKColors.Transparent);
        }
        else
        {
            var color = SKColor.Parse(options.BackgroundColor);
            canvas.Clear(color);
        }

        // Вычисляем размер с сохранением пропорций
        float ratio = Math.Min((float)size / original.Width, (float)size / original.Height);
        int newWidth = (int)(original.Width * ratio);
        int newHeight = (int)(original.Height * ratio);

        int x = (size - newWidth) / 2;
        int y = (size - newHeight) / 2;

        using var resized = original.Resize(
            new SKImageInfo(newWidth, newHeight),
            SKFilterQuality.High);

        canvas.DrawBitmap(resized, new SKPoint(x, y));

        using var image = surface.Snapshot();
        using var data = EncodeImage(image, options.OutputFormat, options.Quality);

        return data.ToArray();
    }

    private byte[] StretchToSquare(SKBitmap original, int size, ImageProcessingOptions options)
    {
        using var resized = original.Resize(
            new SKImageInfo(size, size),
            SKFilterQuality.High);

        using var image = SKImage.FromBitmap(resized);
        using var data = EncodeImage(image, options.OutputFormat, options.Quality);

        return data.ToArray();
    }

    private SKBitmap ResizeImage(SKBitmap original, int? width, int? height, bool maintainAspectRatio)
    {
        int newWidth = width ?? original.Width;
        int newHeight = height ?? original.Height;

        if (maintainAspectRatio)
        {
            float ratio = Math.Min(
                (float)newWidth / original.Width,
                (float)newHeight / original.Height);

            newWidth = (int)(original.Width * ratio);
            newHeight = (int)(original.Height * ratio);
        }

        return original.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
    }

    private SKBitmap CropToSquareBitmap(SKBitmap original, CropMode mode)
    {
        if (mode == CropMode.Stretch)
            return original;

        int cropSize = Math.Min(original.Width, original.Height);
        int x = (original.Width - cropSize) / 2;
        int y = (original.Height - cropSize) / 2;

        var cropRect = new SKRectI(x, y, x + cropSize, y + cropSize);
        var cropped = new SKBitmap(cropSize, cropSize);
        original.ExtractSubset(cropped, cropRect);

        return cropped;
    }

    private SKData EncodeImage(SKImage image, OutputFormat format, int quality)
    {
        return format switch
        {
            OutputFormat.Png => image.Encode(SKEncodedImageFormat.Png, quality),
            OutputFormat.Jpeg => image.Encode(SKEncodedImageFormat.Jpeg, quality),
            OutputFormat.WebP => image.Encode(SKEncodedImageFormat.Webp, quality),
            _ => image.Encode(SKEncodedImageFormat.Png, quality)
        };
    }

    private string GetContentType(OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Png => "image/png",
            OutputFormat.Jpeg => "image/jpeg",
            OutputFormat.WebP => "image/webp",
            _ => "application/octet-stream"
        };
    }

    #endregion
}

// ImageProcessingService.Core/Models/ServiceConfiguration.cs
public class ServiceConfiguration
{
    public int MaxImageSize { get; set; } = 10 * 1024 * 1024; // 10MB
    public int DefaultIconSize { get; set; } = 42;
    public int DefaultQuality { get; set; } = 90;
    public string TempDirectory { get; set; } = "temp";
    public bool EnableCaching { get; set; } = false;
}