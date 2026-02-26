using LP.Entity;
using LP.Server.Services.ImageProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Drawing;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PhotosController : BaseAuthController
    {
        private readonly ApplicationContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IImageProcessingService _imageService;

        public PhotosController(IWebHostEnvironment env, ApplicationContext context, IImageProcessingService imageService) 
        {
            _env = env;
            _context = context;
            _imageService = imageService;
        }

        [Authorize]
        [HttpGet("list")]
        //[ResponseCache(Duration = 60)] // Cache for 1 minute
        public async Task<IActionResult> GetImageList()
        {
            var favor = await _context.PhotoMain.Where(x => x.User.Id == UserId).Select(f=>f.PhotoId).ToListAsync();
            var res = await _context.Photos.Where(x => x.User.Id == UserId).Take(20).ToListAsync();
            var orderList = res
                .OrderByDescending(x=> favor.Contains(x.Id))
                .ToList();
            return Ok(res);
        }

        [AllowAnonymous]
        [HttpGet("image/{id}")]
        public async Task<IActionResult> GetImage(Guid id) // here is photo Id
        {
            var sw = Stopwatch.StartNew();
            var photo = await _context.Photos
                .AsNoTracking()
                .Select(x => new { x.Id, UserId = x.User.Id, x.Path })
                .FirstOrDefaultAsync(x => x.Id == id);

            if (photo == null)
                return NotFound();

            var userId = _context.Photos.Where(x => x.Id == id).Select(x => x.User.Id).FirstOrDefault();
            //var imgPath = _context.Photos.Where(x => x.Id == id).Select(x => x.Path).FirstOrDefault();
            string imgPath = Path.Combine(_env.ContentRootPath, "..", "img" , userId.ToString(), id.ToString());
            if (!System.IO.File.Exists(imgPath))
            {
                if (string.IsNullOrWhiteSpace(photo.Path) || !Uri.IsWellFormedUriString(photo.Path, UriKind.Absolute))
                {
                    var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Host;
                    var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;

                    return Redirect($"{scheme}://{host}/assets/default-avatar.svg");
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(imgPath));
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
                    Console.WriteLine($"Ошибка загрузки изображения {id}: {ex.Message}");
                    return NotFound();
                }
            }

            sw.Stop();
            Console.WriteLine($"Время выполнения: {sw.ElapsedMilliseconds} мс");
            return PhysicalFile(imgPath, "application/octet-stream", enableRangeProcessing: true);
        }

        [AllowAnonymous]
        [HttpGet("imagefree")]
        public IActionResult GetImageFree([FromQuery] Guid id)
        {
            string imgPath = Path.Combine(_env.ContentRootPath, "..", "img\\scroll", id.ToString());
            if (!System.IO.File.Exists(imgPath))
                return NotFound();
            return PhysicalFile(imgPath, "application/octet-stream", enableRangeProcessing: true);
        }

        [AllowAnonymous]
        [HttpGet("back/{name}")]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public IActionResult GetImageBack(string name)
        {
            string imgPath = Path.Combine(_env.ContentRootPath, "..", "img\\back", name);
            if (!System.IO.File.Exists(imgPath))
                return NotFound();

            var ext = Path.GetExtension(name).ToLower();
            var mimeType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

            // Добавляем заголовки для кеширования
            Response.Headers.Add("Cache-Control", "public, max-age=86400");
            Response.Headers.Add("Expires", DateTime.UtcNow.AddDays(1).ToString("R"));

            return PhysicalFile(imgPath, mimeType, enableRangeProcessing: true);
        }

        [AllowAnonymous]
        [HttpGet("scroll")]
        public async Task<IActionResult> GetImages()
        {
            string imgPath = Path.Combine(_env.ContentRootPath, "..", "img\\scroll");
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
                .Select(x=> new { PhotoId = x.Id, UserId = x.User.Id, x.User.Created })
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
                Directory.GetFiles(imgPath).Select(Path.GetFileNameWithoutExtension).Where(x=>x != "avatar")
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
            await using var stream = new FileStream(imgPath, FileMode.Create);
            await file.CopyToAsync(stream);

            

            // 🔥 Проверяем, является ли это первым фото пользователя
            var userPhotoCount = await _context.Photos.CountAsync(x => x.User.Id == UserId);
            if (userPhotoCount == 0)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var icon = await _imageService.CreateSquareIconAndSaveAsync(ms.ToArray(), Path.Combine(userPath, "avatar.jpg"));

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
            var photo = new Photo() { Id = id, User = _user};

            // Привязать и удалить
            _context.Photos.Attach(photo);
            _context.Photos.Remove(photo);

            string imgPath = Path.Combine(_env.ContentRootPath, "..", "img", UserId.ToString(), id.ToString());
            if (System.IO.File.Exists(imgPath)) System.IO.File.Delete(imgPath);

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
            
            
            var photo = new PhotoMain(){PhotoId = id, User = user};
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

        [Authorize]
        [HttpGet("avatar/{id}")]
        public IActionResult Avatar(Guid id)
        {
            string imgPath = Path.Combine(_env.ContentRootPath, "..", "img", id.ToString(), "avatar.jpg");
            if (!System.IO.File.Exists(imgPath))
                return NotFound();
            return PhysicalFile(imgPath, "application/octet-stream", enableRangeProcessing: true);
        }
    }
}
