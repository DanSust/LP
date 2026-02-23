using LP.Common;
using LP.Entity;
using LP.Server.DTO;
using LP.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
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
            // Основные данные пользователя
            var user = await _context.Users
                .Select(x => new
                {
                    x.Id,
                    x.Caption,
                    x.Sex,
                    x.IsPaused,
                    x.Birthday
                })
                .FirstOrDefaultAsync(x => x.Id == id);

            if (user == null)
                return NotFound();

            // Профиль отдельно
            var profile = await _context.Profiles
                .Where(x => x.UserId == id)
                .Select(x => new
                {
                    x.Description,
                    x.Weight,
                    x.Height,
                    x.AgeFrom,
                    x.AgeTo,
                    x.Aim,
                    x.SendEmail,
                    x.WithPhoto,
                    x.WithEmail,
                    x.WithLikes,
                    x.CityId
                })
                .FirstOrDefaultAsync();

            // Название города отдельно (если профиль есть)
            string? cityName = null;
            if (profile != null)
            {
                cityName = await _context.Cities
                    .Where(x => x.Id == profile.CityId)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync();
            }

            // Интересы отдельно
            var interests = await _context.UserInterests
                .Where(x => x.User.Id == id)
                .OrderBy(x => x.Order)
                .Select(x => new
                {
                    x.Interest.Id,
                    x.Interest.Name,
                    x.Interest.Path,
                    x.Interest.Group,
                    x.Order
                })
                .ToListAsync();

            // Сборка результата
            var result = new
            {
                user.Id,
                user.Caption,
                user.Sex,
                user.IsPaused,
                user.Birthday,
                // Profile (прямо в корне, null если нет)
                Description = profile?.Description,
                Weight = profile?.Weight,
                Height = profile?.Height,
                AgeFrom = profile?.AgeFrom,
                AgeTo = profile?.AgeTo,
                Aim = profile?.Aim,
                WithLikes = profile?.WithLikes ?? false,
                CityId = profile?.CityId,
                CityName = cityName,

                // Interests (массив в корне)
                Interests = interests
            };

            return Ok(result);
        }

        [HttpGet("info")]
        [Authorize]
        public async Task<IActionResult> Details()
        {
            var profile = await _context.Profiles.FirstOrDefaultAsync(x => x.UserId == UserId);
            if (profile== null)
            {
                profile = new Profile()
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
                    Aim = Aim.aimLater
                };
                _context.Profiles.Add(profile);
                await _context.SaveChangesAsync();
            }
            var town = profile.CityId != default
                ? await _context.Cities.FirstOrDefaultAsync(x => x.Id == profile.CityId)
                : null;
            var interests = await _context.UserInterests
                .Where(x => x.User.Id == UserId)
                .Include(x=>x.Interest)
                .Select(x=>x.Interest).ToListAsync();
            var user = await _context.Users
                .Select(x => new
                {
                    Id = x.Id,
                    Email = x.Email,
                    Caption = x.Caption,
                    Sex = x.Sex,
                    IsPaused = x.IsPaused,
                    Provider = x.Provider,
                    Birthday = x.Birthday != null ? x.Birthday : DateOnly.FromDateTime(DateTime.Now.AddYears(-20)),
                    Description = profile != null ? profile.Description : null,
                    Weight = profile != null ? profile.Weight : 80,
                    Height = profile != null ? profile.Height : 180,
                    AgeFrom = profile != null ? profile.AgeFrom : 18,
                    AgeTo = profile != null ? profile.AgeTo : 80,
                    townId = town != null ? town.Id : Guid.Empty,
                    townName = town != null ? town.Name : "",
                    Aim = profile != null ? profile.Aim : Aim.aimLater,
                    SendEmail = profile != null ? profile.SendEmail : true,
                    SendETelegram = profile != null ? profile.SendTelegram : true,
                    WithPhoto = profile != null ? profile.WithPhoto : true,
                    WithEmail = profile != null ? profile.WithEmail : true,
                    WithLikes = profile != null ? profile.WithLikes : false,
                    interests = interests 
                })
                .FirstOrDefaultAsync(x => x.Id == UserId);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user);
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
