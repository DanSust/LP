// ImageProcessingService.Core/Models/ImageProcessingOptions.cs
namespace LP.Server.Services.ImageProcessing;

public class ImageProcessingOptions
{
    /// <summary>
    /// Качество сжатия (1-100)
    /// </summary>
    public int Quality { get; set; } = 90;

    /// <summary>
    /// Формат выходного изображения
    /// </summary>
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Png;

    /// <summary>
    /// Режим обрезки для создания квадрата
    /// </summary>
    public CropMode CropMode { get; set; } = CropMode.Center;

    /// <summary>
    /// Цвет фона (для режима Pad)
    /// </summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Сохранять ли метаданные
    /// </summary>
    public bool PreserveMetadata { get; set; } = false;
}

public enum OutputFormat
{
    Png,
    Jpeg,
    WebP
}

public enum CropMode
{
    /// <summary>
    /// Обрезка по центру
    /// </summary>
    Center,

    /// <summary>
    /// Добавление полей
    /// </summary>
    Pad,

    /// <summary>
    /// Растяжение (с искажением пропорций)
    /// </summary>
    Stretch
}

// ImageProcessingService.Core/Models/ProcessingParameters.cs
public class ProcessingParameters
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int Quality { get; set; } = 90;
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Jpeg;
    public bool MaintainAspectRatio { get; set; } = true;
    public bool CropToSquare { get; set; } = false;
    public CropMode SquareCropMode { get; set; } = CropMode.Center;
}

// ImageProcessingService.Core/Models/ProcessingResult.cs
public class ProcessingResult
{
    public byte[] Data { get; set; }
    public string ContentType { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long SizeInBytes { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
}