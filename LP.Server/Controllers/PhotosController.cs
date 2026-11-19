using LP.Entity;
using LP.Server.Services.ImageProcessing;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PhotosController : BaseAuthController
    {
        private const long OptimizationThresholdBytes = 300 * 1024;
        private const int MaxImageDimension = 1600;
        private const int OptimizedImageQuality = 80;
        private const string ImageCacheControl = "public, max-age=604800";

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> OptimizationLocks = new(StringComparer.OrdinalIgnoreCase);

        private readonly IWebHostEnvironment _env;
        private readonly IImageProcessingService _imageService;
        private readonly ILogger<PhotosController> _logger;

        private string SafeCombine(params string[] paths)
        {
            return Path.Combine(paths.Where(p => !string.IsNullOrEmpty(p)).ToArray());
        }

        public PhotosController(
            IWebHostEnvironment env,
            ApplicationContext context,
            IImageProcessingService imageService,
            ILogger<PhotosController> logger) : base(context)
        {
            _env = env;
            _imageService = imageService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("list")]
        //[ResponseCache(Duration = 60)] // Cache for 1 minute
        public async Task<IActionResult> GetImageList()
        {
            var favor = await _context.PhotoMain.Where(x => x.User.Id == UserId).Select(f => f.PhotoId).ToListAsync();
            var res = await _context.Photos.Where(x => x.User.Id == UserId).Take(20).ToListAsync();
            var orderList = res
                .OrderByDescending(x => favor.Contains(x.Id))
                .ToList();
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpGet("image/{id}")]
        public async Task<IActionResult> GetImage(Guid id) // here is photo Id
        {
            var photo = await _context.Photos
                .AsNoTracking()
                .Select(x => new { x.Id, UserId = x.User.Id, x.Path })
                .FirstOrDefaultAsync(x => x.Id == id);

            if (photo == null)
                return NotFound();

            var userDir = Path.Combine(_env.ContentRootPath, "..", "img", photo.UserId.ToString());
            var safeId = id.ToString("D");
            var imgPath = FindImageFile(userDir, safeId);

            if (imgPath == null)
            {
                if (string.IsNullOrWhiteSpace(photo.Path) || !Uri.IsWellFormedUriString(photo.Path, UriKind.Absolute))
                {
                    var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Host;
                    var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;

                    return Redirect($"{scheme}://{host}/assets/default-avatar.svg");
                }

                imgPath = Path.Combine(userDir, safeId);
                try
                {
                    Directory.CreateDirectory(userDir);
                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    var response = await httpClient.GetAsync(photo.Path);

                    if (!response.IsSuccessStatusCode)
                        return NotFound();

                    // Прямое сохранение без промежуточного файла
                    await using var fs = new FileStream(imgPath, FileMode.Create, FileAccess.Write);
                    await response.Content.CopyToAsync(fs);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка загрузки изображения {PhotoId}", id);
                    return NotFound();
                }
            }

            var (servedPath, contentType) = await GetServedImageAsync(imgPath);
            Response.Headers.CacheControl = ImageCacheControl;
            return PhysicalFile(servedPath, contentType, enableRangeProcessing: true);
        }

        [AllowAnonymous]
        [HttpGet("imagefree")]
        public async Task<IActionResult> GetImageFree([FromQuery] Guid id)
        {
            var scrollDir = Path.Combine(_env.ContentRootPath, "..", "img", "scroll");
            var imgPath = FindImageFile(scrollDir, id.ToString("D"));
            if (imgPath == null)
                return NotFound();

            var (servedPath, contentType) = await GetServedImageAsync(imgPath);
            Response.Headers.CacheControl = ImageCacheControl;
            return PhysicalFile(servedPath, contentType, enableRangeProcessing: true);
        }

        [AllowAnonymous]
        [HttpGet("back/{name}")]
        //[ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public IActionResult GetImageBack(string name)
        {
            _logger.LogInformation("Getting background image {Name}", name);

            var safeName = Path.GetFileName(name);
            var possiblePaths = new List<string>();
            try
            {
                // Только не-null пути
                possiblePaths.Add(SafeCombine(_env.ContentRootPath, "img", "back", safeName));
                possiblePaths.Add(SafeCombine(_env.WebRootPath, "img", "back", safeName));
                possiblePaths.Add(SafeCombine(_env.ContentRootPath, "..", "img", "back", safeName));
                possiblePaths.Add(SafeCombine("/app", "img", "back", safeName));


                string? imgPath = possiblePaths.FirstOrDefault(System.IO.File.Exists);
                if (imgPath == null)
                {
                    _logger.LogWarning("Background image {Name} not found in any location", name);
                    return NotFound();
                }

                var ext = Path.GetExtension(name).ToLowerInvariant();
                var mimeType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => DetectImageContentType(imgPath)
                };

                _logger.LogDebug("Serving background {Name} from {Path}", name, imgPath);
                Response.Headers.CacheControl = ImageCacheControl;
                return PhysicalFile(imgPath, mimeType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                //_logger.Log(LogLevel.Error, "Exit GetImageBack:" + ex.Message);
                _logger.LogError(ex, "Error serving background image {Name}", name);
                return Ok();
            }
        }

        [AllowAnonymous]
        [HttpGet("scroll")]
        //[ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> GetImages()
        {
            string imgPath = Path.Combine(_env.ContentRootPath, "..", "img", "scroll");
            var fileIdsOnDisk = await Task.Run(() =>
                Directory.GetFiles(imgPath)
                    .Select(f => new
                    {
                        FileName = Path.GetFileNameWithoutExtension(f),
                        CreationTime = System.IO.File.GetCreationTime(f)
                    })
                    .OrderByDescending(x => x.CreationTime)
                    .Select(x => x.FileName)
                    .ToList()
            );

            var photoIdsInDb = await _context.Photos
                .Where(x => fileIdsOnDisk.Contains(x.Id.ToString()))
                .Select(x => new { PhotoId = x.Id, UserId = x.User.Id, x.User.Created })
                .ToListAsync();

            var result = await Task.Run(() =>
                fileIdsOnDisk.Select(id => new
                {
                    PhotoId = id,
                    UserId = photoIdsInDb.FirstOrDefault(p => p.PhotoId.ToString() == id)?.UserId ?? Guid.NewGuid(),
                    ExistsInDb = photoIdsInDb.Any(p => p.PhotoId.ToString() == id),
                    CreatedAt = photoIdsInDb.FirstOrDefault(p => p.PhotoId.ToString() == id)?.Created ?? DateTime.MinValue
                })
                    .Where(x => x.UserId != UserId)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToList()
            );
            return Ok(result);
        }

        [Authorize]
        [HttpGet("user")]
        public async Task<IActionResult> GetUserImages([FromQuery] Guid id)
        {
            string imgPath = Path.Combine(_env.ContentRootPath, "..", "img", id.ToString());
            if (!System.IO.Directory.Exists(imgPath))
                return NotFound();
            var result = await Task.Run(() =>
                Directory.GetFiles(imgPath).Select(Path.GetFileNameWithoutExtension).Where(x => x != "avatar")
                    .ToList());

            return Ok(result.Select(x => new { id = x }).ToList());
        }

        [Authorize]
        [HttpPost("add")]
        public async Task<IActionResult> AddImage([FromForm] IFormFile file)
        {
            User _user = _context.Users.FirstOrDefault(x => x.Id == UserId);
            Guid _id = Guid.NewGuid();

            string userPath = Path.Combine(_env.ContentRootPath, "..", "img", UserId.ToString());
            if (!Directory.Exists(userPath))
                Directory.CreateDirectory(userPath);

            string imgPath = Path.Combine(userPath, _id.ToString());
            {
                await using var stream = new FileStream(imgPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await file.CopyToAsync(stream);
                await stream.FlushAsync(); // Принудительно сбрасываем буферы
            }

            // 🔥 Проверяем, является ли это первым фото пользователя
            var userPhotoCount = await _context.Photos.CountAsync(x => x.User.Id == UserId);
            if (userPhotoCount == 0)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var icon = await _imageService.CreateSquareIconAndSaveAsync(ms.ToArray(), Path.Combine(userPath, "avatar.jpg"), 128);

                var photoMain = new PhotoMain() { PhotoId = _id, User = _user };
                _context.PhotoMain.Add(photoMain);

                string scrollPath = Path.Combine(_env.ContentRootPath, "..", "img", "scroll");

                // Создаём папку если нет
                if (!Directory.Exists(scrollPath))
                    Directory.CreateDirectory(scrollPath);

                // Получаем список файлов с датой создания
                var scrollFiles = new DirectoryInfo(scrollPath)
                    .GetFiles()
                    .OrderBy(f => f.CreationTime)
                    .ToList();

                // Если больше 10 файлов — ищем старый (>1 часа) для удаления
                if (scrollFiles.Count >= 10)
                {
                    var oneHourAgo = DateTime.Now.AddHours(-1);
                    var oldFile = scrollFiles.FirstOrDefault(f => f.CreationTime < oneHourAgo);

                    if (oldFile != null)
                    {
                        try
                        {
                            oldFile.Delete();
                            Console.WriteLine($"Удалён старый файл (>1ч) из scroll: {oldFile.Name}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка удаления старого файла: {ex.Message}");
                            // Если не удалось удалить — всё равно пробуем добавить новый
                        }
                    }
                    else
                    {
                        // Все файлы свежие (<1 часа) — не добавляем в scroll
                        Console.WriteLine("В scroll все файлы свежие (<1ч), новое фото не добавлено");

                        // Сохраняем фото в БД, но не в scroll
                        var _photoOnly = new Photo() { Id = _id, User = _user, Path = imgPath };
                        _context.Photos.Add(_photoOnly);
                        await _context.SaveChangesAsync();
                        return Ok(new { userId = _photoOnly.User.Id, id = _photoOnly.Id, addedToScroll = false });
                    }
                }

                // Копируем новое фото в scroll
                string targetPath = Path.Combine(scrollPath, _id.ToString());
                try
                {
                    System.IO.File.Copy(imgPath, targetPath);
                    Console.WriteLine($"Добавлено в scroll: {_id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка копирования в scroll: {ex.Message}");
                }
            }

            var _photo = new Photo() { Id = _id, User = _user, Path = imgPath };
            _context.Photos.Add(_photo);
            await _context.SaveChangesAsync();

            return Ok(new { userId = _photo.User.Id, id = _photo.Id, addedToScroll = userPhotoCount == 0 });
        }

        [Authorize]
        [HttpPost("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Создать "пустую" сущность только с ID
            User _user = _context.Users.FirstOrDefault(x => x.Id == UserId);
            var photo = new Photo() { Id = id, User = _user };

            // Привязать и удалить
            _context.Photos.Attach(photo);
            _context.Photos.Remove(photo);

            var userDir = Path.Combine(_env.ContentRootPath, "..", "img", UserId.ToString());
            var imgPath = FindImageFile(userDir, id.ToString("D"));
            if (imgPath != null)
            {
                var cachePath = Path.Combine(userDir, ".cache", Path.GetFileName(imgPath) + ".webp");
                if (System.IO.File.Exists(cachePath))
                    System.IO.File.Delete(cachePath);

                System.IO.File.Delete(imgPath);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    error = ex.Message
                });
            }

            return Ok(id);
        }

        [Authorize]
        [HttpPost("order/{id}")]
        public async Task<IActionResult> Order(Guid id)
        {
            var user = _context.Users.FindAsync(UserId).Result;

            _context.PhotoMain.Where(x => x.User.Id == user.Id).ExecuteDelete();


            var photo = new PhotoMain() { PhotoId = id, User = user };
            _context.PhotoMain.Add(photo);

            await _context.SaveChangesAsync();

            return Ok(id);
        }

        [Authorize]
        [HttpPost("main/{id}")]
        public async Task<IActionResult> MainPhoto(Guid id)
        {
            var result = await _context.PhotoMain.Where(x => x.User.Id == id).FirstOrDefaultAsync();

            return Ok(result?.PhotoId);
        }

        /// <summary>
        /// Создаёт аватар из уже обрезанного квадрата, присланного клиентом.
        /// Отдельный метод: AddImage сохраняет аватар только из первого загруженного фото,
        /// а здесь пользователь сам выбирает область кадрирования.
        /// </summary>
        [Authorize]
        [HttpPost("avatar")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Файл не загружен" });

            string userPath = Path.Combine(_env.ContentRootPath, "..", "img", UserId.ToString());
            if (!Directory.Exists(userPath))
                Directory.CreateDirectory(userPath);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            await _imageService.CreateSquareIconAndSaveAsync(
                ms.ToArray(), Path.Combine(userPath, "avatar.jpg"), 128);

            return Ok(new { avatar = $"/Photos/avatar/{UserId}" });
        }

        [Authorize]
        [HttpGet("avatar/{id}")]
        public async Task<IActionResult> Avatar(Guid id)
        {
            string imgPath = Path.Combine(_env.ContentRootPath, "..", "img", id.ToString(), "avatar.jpg");
            if (!System.IO.File.Exists(imgPath))
                return NotFound();

            var (servedPath, contentType) = await GetServedImageAsync(imgPath);
            Response.Headers.CacheControl = ImageCacheControl;
            return PhysicalFile(servedPath, contentType, enableRangeProcessing: true);
        }

        private static string? FindImageFile(string directory, string id)
        {
            var candidates = new[]
            {
                id,
                id + ".jpg",
                id + ".jpeg",
                id + ".png",
                id + ".webp",
                id + ".gif"
            };

            return candidates
                .Select(name => Path.Combine(directory, name))
                .FirstOrDefault(System.IO.File.Exists);
        }

        private bool ClientAcceptsWebP()
        {
            var accept = Request.Headers.Accept.ToString();
            return accept.Contains("image/webp", StringComparison.OrdinalIgnoreCase)
                || accept.Contains("image/*", StringComparison.OrdinalIgnoreCase)
                || accept.Contains("*/*", StringComparison.OrdinalIgnoreCase);
        }

        private static string DetectImageContentType(string path)
        {
            Span<byte> header = stackalloc byte[12];
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var read = stream.Read(header);

                if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                    return "image/jpeg";

                if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                    return "image/png";

                if (read >= 6 && header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'8')
                    return "image/gif";

                if (read >= 12 && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                    && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
                    return "image/webp";

                if (read >= 2 && header[0] == (byte)'B' && header[1] == (byte)'M')
                    return "image/bmp";

                if (read >= 12 && header[4] == (byte)'f' && header[5] == (byte)'t' && header[6] == (byte)'y' && header[7] == (byte)'p')
                {
                    var brand = Encoding.ASCII.GetString(header.Slice(8, 4));
                    if (brand is "heic" or "heix" or "hevc" or "hevx" or "mif1" or "msf1")
                        return "image/heic";
                }
            }
            catch (IOException)
            {
                // Файл может быть занят другим процессом — отдадим безопасный тип.
            }
            catch (UnauthorizedAccessException)
            {
            }

            return "application/octet-stream";
        }

        private async Task<(string Path, string ContentType)> GetServedImageAsync(string originalPath)
        {
            var originalContentType = DetectImageContentType(originalPath);

            if (!ClientAcceptsWebP()
                || originalContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            {
                return (originalPath, originalContentType);
            }

            var originalInfo = new FileInfo(originalPath);
            if (!originalInfo.Exists || originalInfo.Length <= OptimizationThresholdBytes)
                return (originalPath, originalContentType);

            var originalDirectory = Path.GetDirectoryName(originalPath);
            if (string.IsNullOrEmpty(originalDirectory))
                return (originalPath, originalContentType);

            var cacheDirectory = Path.Combine(originalDirectory, ".cache");
            var cachePath = Path.Combine(cacheDirectory, Path.GetFileName(originalPath) + ".webp");
            var cacheInfo = new FileInfo(cachePath);
            if (cacheInfo.Exists && cacheInfo.LastWriteTimeUtc >= originalInfo.LastWriteTimeUtc)
                return (cachePath, "image/webp");

            var gate = OptimizationLocks.GetOrAdd(cachePath, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(HttpContext.RequestAborted);
            try
            {
                cacheInfo.Refresh();
                originalInfo.Refresh();

                if (cacheInfo.Exists && cacheInfo.LastWriteTimeUtc >= originalInfo.LastWriteTimeUtc)
                    return (cachePath, "image/webp");

                var originalBytes = await System.IO.File.ReadAllBytesAsync(originalPath, HttpContext.RequestAborted);
                var result = await _imageService.ProcessImageAsync(originalBytes, new ProcessingParameters
                {
                    Width = MaxImageDimension,
                    Height = MaxImageDimension,
                    MaintainAspectRatio = true,
                    AllowUpscale = false,
                    OutputFormat = OutputFormat.WebP,
                    Quality = OptimizedImageQuality
                });

                if (!result.Success || result.Data == null || result.Data.Length == 0 || result.Data.Length >= originalBytes.Length)
                {
                    _logger.LogWarning(
                        "Оптимизация не дала выигрыша для {Path}: {Error}",
                        originalPath,
                        result.ErrorMessage);
                    return (originalPath, originalContentType);
                }

                Directory.CreateDirectory(cacheDirectory);
                var tempPath = Path.Combine(cacheDirectory, $"{Path.GetFileName(originalPath)}.{Guid.NewGuid():N}.tmp");
                try
                {
                    await System.IO.File.WriteAllBytesAsync(tempPath, result.Data, HttpContext.RequestAborted);
                    System.IO.File.Move(tempPath, cachePath, overwrite: true);
                    _logger.LogInformation(
                        "Изображение оптимизировано: {Path}, {Before} -> {After} байт",
                        originalPath,
                        originalBytes.Length,
                        result.Data.Length);
                    return (cachePath, "image/webp");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось сохранить оптимизированное изображение {Path}", originalPath);
                    return (originalPath, originalContentType);
                }
                finally
                {
                    if (System.IO.File.Exists(tempPath))
                    {
                        try
                        {
                            System.IO.File.Delete(tempPath);
                        }
                        catch
                        {
                            // Временный файл удалится при следующей очистке.
                        }
                    }
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
