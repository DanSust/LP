using LP.Common;
using LP.Entity;
using LP.Server.DTO;
using LP.Server.Services;
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
    public class UsersController(ApplicationContext context, IAuthService authService) : BaseAuthController
    {
        private readonly ApplicationContext _context = context;
        private readonly IAuthService _authService = authService;

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
            // 🔥 ОДИН запрос вместо 4-х
            var userData = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    User = x,
                    Profile = _context.Profiles
                        .Where(p => p.UserId == x.Id)
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
                        .FirstOrDefault(),
                    CityName = _context.Cities
                        .Where(c => c.Id == _context.Profiles
                            .Where(p => p.UserId == x.Id)
                            .Select(p => p.CityId)
                            .FirstOrDefault())
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                    Interests = _context.UserInterests
                        .Where(ui => ui.User.Id == id)
                        .OrderBy(ui => ui.Order)
                        .Select(ui => new
                        {
                            ui.Interest.Id,
                            ui.Interest.Name,
                            ui.Interest.Path,
                            ui.Interest.Group
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (userData?.User == null)
            {
                return NotFound();
            }

            // 🔥 Профиль может отсутствовать - создаем дефолтный "пустой"
            var profile = userData.Profile;

            var result = new
            {
                userData.User.Id,
                userData.User.Caption,
                userData.User.Sex,
                userData.User.IsPaused,
                userData.User.Birthday,

                // Profile данные (или дефолты)
                Description = profile?.Description,
                Weight = profile?.Weight,
                Height = profile?.Height,
                AgeFrom = profile?.AgeFrom,
                AgeTo = profile?.AgeTo,
                Aim = profile?.Aim,
                WithLikes = profile?.WithLikes ?? false,
                CityId = profile?.CityId,
                CityName = userData.CityName, // 🔥 Уже строка или null

                // Interests
                Interests = userData.Interests
            };

            return Ok(result);
        }

        [HttpGet("info")]
        [Authorize]
        public async Task<IActionResult> Details()
        {
            var userData = await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == UserId)
                .Select(x => new
                {
                    User = x,
                    Profile = _context.Profiles.FirstOrDefault(p => p.UserId == x.Id),
                    TownId = _context.Profiles
                        .Where(p => p.UserId == x.Id)
                        .Select(p => p.CityId)
                        .FirstOrDefault(),
                    TownName = _context.Cities
                        .Where(c => c.Id == _context.Profiles
                            .Where(p => p.UserId == x.Id)
                            .Select(p => p.CityId)
                            .FirstOrDefault())
                        .Select(c => c.Name)
                        .FirstOrDefault(),
                    Interests = _context.UserInterests
                        .Where(ui => ui.User.Id == x.Id)
                        .OrderBy(ui => ui.Order)
                        .Select(ui => new
                        {
                            ui.Interest.Id,
                            ui.Interest.Name,
                            ui.Interest.Path,
                            ui.Interest.Group
                        })
                        .ToList(),
                    IsConfirmed = _context.EmailConfirmations
                        .Any(ec => ec.UserId == x.Id && ec.IsConfirmed)
                })
                .FirstOrDefaultAsync();

            if (userData?.User == null)
            {
                return NotFound();
            }

            // Создаем профиль если отсутствует (ленивая инициализация)
            if (userData.Profile == null)
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

                // Инвалидируем кеш и возвращаем заново
                return await Details();
            }
            

            // 🔥 Исправлено: DateOnly не nullable, проверяем через == default
            var birthday = userData.User.Birthday == default
                ? DateOnly.FromDateTime(DateTime.Now.AddYears(-20))
                : userData.User.Birthday;

            // Формируем ответ
            var result = new
            {
                Id = userData.User.Id,
                Email = userData.User.Email,
                Caption = userData.User.Caption,
                Sex = userData.User.Sex,
                IsPaused = userData.User.IsPaused,
                Provider = userData.User.Provider,
                IsConfirmed = userData.IsConfirmed,
                Birthday = birthday,
                Description = userData.Profile.Description,
                Weight = userData.Profile.Weight,
                Height = userData.Profile.Height,
                AgeFrom = userData.Profile.AgeFrom,
                AgeTo = userData.Profile.AgeTo,
                townId = userData.TownId,
                townName = userData.TownName ?? "",
                Aim = userData.Profile.Aim,
                SendEmail = userData.Profile.SendEmail,
                SendTelegram = userData.Profile.SendTelegram,
                WithPhoto = userData.Profile.WithPhoto,
                WithEmail = userData.Profile.WithEmail,
                WithLikes = userData.Profile.WithLikes,
                interests = userData.Interests
            };

            // Сохраняем в кеш на 5 минут
            //await _cache.SetStringAsync(
            //    cacheKey,
            //    JsonSerializer.Serialize(result),
            //    new DistributedCacheEntryOptions
            //    {
            //        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            //    }
            //);

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
            profile.WithLikes = model.GetProperty("WithLikes").GetBoolean();
            profile.Aim = Enum.Parse<Aim>(model.GetProperty("Aim").GetString());
            profile.CityId = model.GetProperty("TownName").GetProperty("id").GetGuid();

            // Delete all interests
            await _context.UserInterests
                .Where(p => p.User.Id == UserId)
                .ExecuteDeleteAsync();

            foreach (var item in model.GetProperty("interests").EnumerateArray())
            {
                if (!item.TryGetProperty("selected", out var selectedProp) || !selectedProp.GetBoolean())
                    continue;
                var id = item.GetProperty("id").GetGuid();
                var iterest = new UserInterest()
                    {Id = Guid.NewGuid(), User = user, Interest = _context.Interests.FirstOrDefault(x => x.Id == id)};
                _context.UserInterests.Add(iterest);
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

        private bool UserExists(Guid id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}
