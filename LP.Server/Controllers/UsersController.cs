using LP.Common;
using LP.Entity;
using LP.Server.DTO;
using LP.Server.Services;
using LP.Server.Services.Rating;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UsersController(
        ApplicationContext context, 
        IAuthService authService,
        IRatingService ratingService) : BaseAuthController(context)
    {
        private readonly ApplicationContext _context = context;
        private readonly IAuthService _authService = authService;
        private readonly IRatingService _ratingService = ratingService;

        // GET: Users
        [AllowAnonymous]
        [HttpGet("cookies")]
        public async Task<IActionResult> Cookies()
        {
            // If authenticated via cookie, prefer claim
            var userId = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
                return Ok(new { userId });

            // fallback to reading a non-HttpOnly cookie (if you set one)
            if (Request.Cookies.TryGetValue("UserId", out var cookieUserId) && !string.IsNullOrEmpty(cookieUserId))
                return Ok(new { userId = cookieUserId });

            return Ok(new { userId = string.Empty });
        }

        [Authorize]
        [HttpGet("view")]
        public async Task<IActionResult> View([FromQuery] Guid id)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Id, x.Caption, x.Sex, x.IsPaused, x.Birthday })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }

            var profile = await _context.Profiles
                .AsNoTracking()
                .Where(p => p.UserId == id)
                .Select(p => new
                {
                    p.Description,
                    p.Weight,
                    p.Height,
                    p.AgeFrom,
                    p.AgeTo,
                    p.Aim,
                    p.SendEmail,
                    p.WithPhoto,
                    p.WithEmail,
                    p.WithLikes,
                    p.CityId
                })
                .FirstOrDefaultAsync();

            string? cityName = null;
            if (profile != null && profile.CityId != Guid.Empty)
            {
                cityName = await _context.Cities
                    .AsNoTracking()
                    .Where(c => c.Id == profile.CityId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();
            }

            var interests = await _context.UserInterests
                .AsNoTracking()
                .Where(ui => ui.User.Id == id)
                .OrderBy(ui => ui.Order)
                .Select(ui => new
                {
                    ui.Interest.Id,
                    ui.Interest.Name,
                    ui.Interest.Path,
                    ui.Interest.Group
                })
                .ToListAsync();

            var result = new
            {
                user.Id,
                user.Caption,
                user.Sex,
                user.IsPaused,
                user.Birthday,

                Description = profile?.Description,
                Weight = profile?.Weight,
                Height = profile?.Height,
                AgeFrom = profile?.AgeFrom,
                AgeTo = profile?.AgeTo,
                Aim = profile?.Aim,
                WithLikes = profile?.WithLikes ?? false,
                CityId = profile?.CityId,
                CityName = cityName,

                Interests = interests
            };

            return Ok(result);
        }

        [HttpGet("info")]
        [Authorize]
        public async Task<IActionResult> Details()
        {
            //Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] /info STARTED");
            var user = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == UserId)
                .Select(x => new
                {
                    x.Id,
                    x.Email,
                    x.Caption,
                    x.Sex,
                    x.IsPaused,
                    x.Provider,
                    x.Birthday
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }

            var profile = await _context.Profiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == UserId);

            // Создаем профиль если отсутствует (ленивая инициализация)
            if (profile == null)
            {
                var newProfile = new Profile
                {
                    UserId = UserId,
                    Description = "",
                    Weight = 80,
                    Height = 180,
                    AgeFrom = 18,
                    AgeTo = 80,
                    SendEmail = true,
                    SendTelegram = true,
                    WithPhoto = true,
                    WithEmail = true,
                    WithLikes = false,
                    Aim = Aim.aimLater,
                    CityId = default
                };

                _context.Profiles.Add(newProfile);
                await _context.SaveChangesAsync();

                profile = newProfile;
            }

            string? townName = null;
            if (profile.CityId != Guid.Empty)
            {
                townName = await _context.Cities
                    .AsNoTracking()
                    .Where(c => c.Id == profile.CityId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();
            }

            var interests = await _context.UserInterests
                .AsNoTracking()
                .Where(ui => ui.User.Id == UserId)
                .OrderBy(ui => ui.Order)
                .Select(ui => new
                {
                    ui.Interest.Id,
                    ui.Interest.Name,
                    ui.Interest.Path,
                    ui.Interest.Group
                })
                .ToListAsync();

            var isConfirmed = await _context.EmailConfirmations
                .AsNoTracking()
                .AnyAsync(ec => ec.UserId == UserId && ec.IsConfirmed);

            var birthday = user.Birthday == default
                ? DateOnly.FromDateTime(DateTime.Now.AddYears(-20))
                : user.Birthday;

            var result = new
            {
                Id = user.Id,
                Email = user.Email,
                Caption = user.Caption,
                Sex = user.Sex,
                IsPaused = user.IsPaused,
                Provider = user.Provider,
                IsConfirmed = isConfirmed,
                Birthday = birthday,
                Description = profile.Description,
                Weight = profile.Weight,
                Height = profile.Height,
                AgeFrom = profile.AgeFrom,
                AgeTo = profile.AgeTo,
                townId = profile.CityId,
                townName = townName ?? "",
                Aim = profile.Aim,
                SendEmail = profile.SendEmail,
                SendTelegram = profile.SendTelegram,
                WithPhoto = profile.WithPhoto,
                WithEmail = profile.WithEmail,
                WithLikes = profile.WithLikes,
                interests = interests
            };

            //Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] /info COMPLETED: {System.Text.Json.JsonSerializer.Serialize(result)}");

            return Ok(result);
        }

        // GET: Users/Create
        //public IActionResult Create()
        //{
        //    return NotFound();
        //}

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Caption,Birthday,Sex,Id")] User user)
        {
            if (ModelState.IsValid)
            {
                user.Id = Guid.NewGuid();
                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return NotFound();
        }

        [HttpPost("pause")]
        //[ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Pause()
        {
            var user = await _context.Users.FindAsync(UserId);
            if (user != null)
            {
                user.IsPaused = !user.IsPaused;
                _context.Users.Update(user);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }


        [HttpPost ("delete")]
        //[ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            // Validate credentials here (e.g., check database)
            var token = _authService.Authenticate(model.Username, model.Password);
            return Ok(new { token });
        }

        [HttpPost("save")]
        [Authorize]
        public async Task<IActionResult> Save([FromBody] JsonElement model)
        {
            var user = await _context.Users.FindAsync(UserId);
            if (user == null) return BadRequest();
            var profile = await _context.Profiles.FirstOrDefaultAsync(x=>x.UserId == UserId);
            
            if (user != null)
            {
                user.Email = model.GetProperty("Email").ToString();
                user.Caption = model.GetProperty("Caption").ToString();
                user.Sex = model.GetProperty("Sex").ToString().ToBool();
                user.Birthday = DateOnly.FromDateTime(model.GetProperty("Birthday").GetDateTime());
                _context.Users.Update(user);
            }

            if (profile == null)
            {
                // Создать новый профиль
                profile = new Profile()
                {
                    UserId = UserId
                };
                _context.Profiles.Add(profile);
            }

            profile.Description = model.GetProperty("Description").ToString();
            profile.Weight = model.GetProperty("Weight").GetInt16();
            profile.Height = model.GetProperty("Height").GetInt16();
            profile.AgeFrom = model.GetProperty("AgeFrom").GetInt16();
            profile.AgeTo = model.GetProperty("AgeTo").GetInt16();
            profile.SendEmail = model.GetProperty("SendEmail").GetBoolean();
            profile.SendTelegram = model.GetProperty("SendTelegram").GetBoolean();
            profile.WithPhoto = model.GetProperty("WithPhoto").GetBoolean();
            profile.WithEmail = model.GetProperty("WithEmail").GetBoolean();
            profile.WithLikes = model.GetProperty("WithLikes").GetBoolean();
            profile.Aim = Enum.Parse<Aim>(model.GetProperty("Aim").GetString());

            // Город приходит объектом TownName { id, name }. Если его нет —
            // пробуем запасное поле Town, чтобы не ронять сохранение.
            if (model.TryGetProperty("TownName", out var townProp) &&
                townProp.ValueKind == JsonValueKind.Object &&
                townProp.TryGetProperty("id", out var townIdProp) &&
                townIdProp.TryGetGuid(out var parsedTownId))
            {
                profile.CityId = parsedTownId;
            }
            else if (model.TryGetProperty("Town", out var townFallback) &&
                     townFallback.ValueKind == JsonValueKind.String &&
                     Guid.TryParse(townFallback.GetString(), out var fallbackTownId))
            {
                profile.CityId = fallbackTownId;
            }

            // Delete all interests
            await _context.UserInterests
                .Where(p => p.User.Id == UserId)
                .ExecuteDeleteAsync();

            var selectedInterestIds = new List<Guid>();
            foreach (var item in model.GetProperty("interests").EnumerateArray())
            {
                if (!item.TryGetProperty("selected", out var selectedProp) || !selectedProp.GetBoolean())
                    continue;
                selectedInterestIds.Add(item.GetProperty("id").GetGuid());
            }

            if (selectedInterestIds.Count > 0)
            {
                var interestsById = await _context.Interests
                    .Where(i => selectedInterestIds.Contains(i.Id))
                    .ToDictionaryAsync(i => i.Id);

                foreach (var id in selectedInterestIds)
                {
                    if (!interestsById.TryGetValue(id, out var interest))
                        continue;

                    _context.UserInterests.Add(new UserInterest
                    {
                        Id = Guid.NewGuid(),
                        User = user,
                        Interest = interest
                    });
                }
            }

            await _context.UserQuestions
                .Where(p => p.User.Id == UserId)
                .ExecuteDeleteAsync();
            foreach (var item in model.GetProperty("questions").EnumerateArray())
            {
                var questionId = item.GetProperty("id").GetGuid();
                var qname = item.GetProperty("question").GetString();
                var order = item.TryGetProperty("order", out var orderProp) ? orderProp.GetInt32() : 0;
                var question = new UserQuestion() { Id = questionId, User = user, Question = qname, Order = order};
                _context.UserQuestions.Add(question);
            }

            await _context.SaveChangesAsync();

            // Validate credentials here (e.g., check database)
            //var token = _authService.Authenticate(model.Username, model.Password);
            return Ok(new ResponseDto {Result = true, Message = "данные сохранены"});
        }

        private static readonly string[] FemaleNames = {
            "Алевтина","Александра","Алёна","Алина","Алла","Анастасия","Ангелина","Анжела","Анжелика","Анна","Антонида","Антонина","Анфиса","Арина","Валентина","Валерия","Варвара","Василиса","Вера","Вероника","Виктория","Галина","Дарья","Евгения","Екатерина","Елена","Елизавета","Жанна","Зинаида","Зоя","Ирина","Кира","Клавдия","Ксения","Лариса","Лидия","Любовь","Людмила","Маргарита","Марина","Мария","Надежда","Наталья","Нина","Оксана","Ольга","Раиса","Регина","Римма","Светлана","София","Таисия","Тамара","Татьяна","Ульяна","Юлия"
        };

        private static readonly string[] MaleNames = {
            "Александр","Алексей","Альберт","Анатолий","Андрей","Антон","Аркадий","Арсений","Артём","Артем Шабашов","Борис","Вадим","Валентин","Валерий","Василий","Виктор","Виталий","Владимир","Владислав","Вячеслав","Геннадий","Георгий","Герман","Григорий","Даниил","Даниил Кнешус","Денис","Дмитрий","Евгений","Егор","Иван","Игнатий","Игорь","Илья","Колян 🚜","Константин","Лаврентий","Леонид","Лука","Макар","Максим","Матвей","Михаил","Никита","Николай","Николай Шатров","Олег","Роман","Семён","Сергей","Станислав","Степан","Фёдор","Эдуард","Юрий","Ярослав"
        };

        private static string GetRandomNameForSex(bool? sex)
        {
            var random = new Random();
            // В текущей логике: Sex == false => мужское имя, Sex == true => женское имя
            if (sex == false)
            {
                return MaleNames[random.Next(MaleNames.Length)];
            }

            return FemaleNames[random.Next(FemaleNames.Length)];
        }

        [AllowAnonymous]
        [HttpPost("update-sex/{id}")]
        public async Task<IActionResult> UpdateSex(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Sex = !user.Sex;
            await _context.SaveChangesAsync();

            return Ok(new ResponseDto { Result = true, Message = "данные сохранены" });
        }

        [AllowAnonymous]
        [HttpPost("update-name/{id}")]
        public async Task<IActionResult> UpdateName(Guid id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Caption = GetRandomNameForSex(user.Sex);
            await _context.SaveChangesAsync();

            return Ok(new ResponseDto { Result = true, Message = "данные сохранены" });
        }

        [HttpGet("rating/{userId}")]
        [AllowAnonymous] // или [Authorize] - по необходимости
        public async Task<IActionResult> GetUserRating(Guid userId)
        {
            try
            {
                var rating = await _ratingService.CalculateUserRating(userId);
                return Ok(rating);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        private bool UserExists(Guid id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
