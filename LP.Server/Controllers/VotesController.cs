using LP.Common;
using LP.Entity;
using LP.Entity.Store;
using LP.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using NuGet.Protocol.Core.Types;
using System.ComponentModel;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VotesController : BaseAuthController
    {
        private readonly ApplicationContext _context;

        public VotesController(ApplicationContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpPost("like/{id}")]
        public async Task<IActionResult> Like(Guid id)
        {
            // Пытаемся обновить существующую запись (0 или 1)
            var updated = await _context.Votes
                .Where(v => v.Owner == UserId && v.Like == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.IsLike, true)
                    .SetProperty(v => v.IsReject, false)
                    .SetProperty(v => v.IsViewed, false)
                    .SetProperty(v => v.Added, DateTime.UtcNow));

            if (updated == 0)
            {
                // Записи не было — создаём новую
                _context.Votes.Add(new Vote
                {
                    Owner = UserId,
                    Like = id,
                    IsLike = true,
                    IsReject = false,
                    IsViewed = false,
                    Added = DateTime.UtcNow
                });

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // Race condition: другой поток уже создал — обновляем
                    await _context.Votes
                        .Where(v => v.Owner == UserId && v.Like == id)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(v => v.IsLike, true)
                            .SetProperty(v => v.IsReject, false)
                            .SetProperty(v => v.IsViewed, false)
                            .SetProperty(v => v.Added, DateTime.UtcNow));
                }
            }

            return Ok(new { message = "OK" });
        }

        [Authorize]
        [HttpPost("dislike/{id}")]
        public async Task<IActionResult> Dislike(Guid id)
        {
            var updated = await _context.Votes
                .Where(v => v.Owner == UserId && v.Like == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.IsLike, false)
                    .SetProperty(v => v.IsReject, true)
                    .SetProperty(v => v.IsViewed, false)
                    .SetProperty(v => v.Added, DateTime.UtcNow));

            if (updated == 0)
            {
                _context.Votes.Add(new Vote
                {
                    Owner = UserId,
                    Like = id,
                    IsLike = false,
                    IsReject = true,
                    IsViewed = false,
                    Added = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "OK" });
        }

        [Authorize]
        [HttpPost("viewed/{id}")]
        public async Task<IActionResult> Viewed(Guid id)
        {
            // Для viewed логика немного другая — не сбрасываем like/reject
            var updated = await _context.Votes
                .Where(v => v.Owner == UserId && v.Like == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.IsViewed, true)
                    .SetProperty(v => v.Added, DateTime.UtcNow));

            if (updated == 0)
            {
                _context.Votes.Add(new Vote
                {
                    Owner = UserId,
                    Like = id,
                    IsLike = false,  // viewed ≠ like
                    IsReject = false,
                    IsViewed = true,
                    Added = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "OK" });
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
            ex.InnerException is SqlException sqlEx &&
            (sqlEx.Number == 2601 || sqlEx.Number == 2627); // SQL Server unique constraint codes

        //[Authorize]
        //[HttpPost("favorite/{id}")]
        //public async Task<IActionResult> Favorite(Guid id)
        //{
        //    var vote = new Vote
        //    {
        //        Owner = UserId,
        //        Like = id,
        //        Favorite = true,
        //        Added = DateTime.Now
        //    };

        //    try
        //    {
        //        _context.Votes.Add(vote);
        //        await _context.SaveChangesAsync();
        //        return Ok(new { message = "Избранный добавлен" });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Conflict(new { message = ex.Message });
        //    }
        //}

        [Authorize]
        [HttpGet("match")]
        public async Task<IActionResult> GetMatches(
            [FromQuery] string? category = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = UserId;

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            // Передаем category в функцию, не используем Where после
            var sql = "SELECT * FROM dbo.GetUserMatches({0}, {1}) ORDER BY LastAdded DESC";

            var allMatches = await _context.MatchResults
                .FromSqlRaw(sql, userId, category ?? (object)DBNull.Value)
                .AsNoTracking()
                .ToListAsync();

            var totalCount = allMatches.Count;

            if (totalCount == 0)
                return Ok(new { items = new List<object>(), hasMore = false, totalCount = 0 });

            // Пагинация в памяти
            var pagedMatches = allMatches
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var targetIds = pagedMatches.Select(m => m.UserId).ToList();

            // Загружаем данные пользователей
            var users = await _context.Users
                .Where(u => targetIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.Caption,
                    u.Birthday,
                    PhotoId = _context.PhotoMain
                        .Where(pm => pm.User.Id == u.Id)
                        .Select(pm => pm.PhotoId)
                        .FirstOrDefault(),
                    Interests = _context.UserInterests
                        .Where(ui => ui.User.Id == u.Id)
                        .Select(ui => ui.Interest.Name)
                        .ToList()
                })
                .ToDictionaryAsync(u => u.Id);

            var today = DateOnly.FromDateTime(DateTime.Now);

            var result = pagedMatches.Select(m =>
            {
                var user = users.GetValueOrDefault(m.UserId);
                return new
                {
                    photoId = user?.PhotoId != Guid.Empty ? user?.PhotoId.ToString() : null,
                    userId = m.UserId,
                    category = m.Category,
                    name = user?.Caption,
                    age = user?.Birthday != null
                        ? today.Year - user.Birthday.Year - (today.DayOfYear < user.Birthday.DayOfYear ? 1 : 0)
                        : 0,
                    //interests = user?.Interests ?? new List<string>()
                };
            }).ToList();

            return Ok(result);
            //return Ok(new
            //{
            //    items = result,
            //    hasMore = (page * pageSize) < totalCount,
            //    totalCount,
            //    currentPage = page,
            //    pageSize
            //});
        }

        [Authorize]
        [HttpGet("list")]
        public async Task<IActionResult> GetVoteList(int count = 5)
        {
            var userId = UserId;

            var currentUserProfile = await _context.Profiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var shownIds = await _context.Votes
                .AsNoTracking()
                .Where(s => s.Owner == userId)
                .Select(s => s.Like)
                .ToListAsync();
            shownIds.Add(userId);

            // Базовый запрос
            var query = _context.Users
                .AsNoTracking()
                .Where(x => x.Username != "admin" && !shownIds.Contains(x.Id) && x.IsPaused == false);

            // Если у текущего пользователя стоит WithEmail - фильтруем только с подтвержденным Email
            if (currentUserProfile?.WithEmail == true)
            {
                query = query.Where(x => x.EmailConfirmation != null && x.EmailConfirmation.IsConfirmed);
            }

            // Если у текущего пользователя стоит WithPhoto - фильтруем только тех, у кого есть фото
            if (currentUserProfile?.WithPhoto == true)
            {
                query = query.Where(x => _context.Photos.Any(p => p.User.Id == x.Id));
            }

            var randomIds = await query
                .OrderBy(u => EF.Functions.Random())
                .Take(count)
                .Select(x => x.Id)
                .ToListAsync();

            if (!randomIds.Any())
                return Ok(new List<object>());

            // 2. Данные пользователей с городами (один запрос)
            var userData = await _context.Users
                .Where(u => randomIds.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.Caption,
                    CityName = _context.Profiles
                        .Where(p => p.UserId == u.Id)
                        .Select(p => _context.Cities.Where(c => c.Id == p.CityId).Select(c => c.Name).FirstOrDefault())
                        .FirstOrDefault()
                })
                .AsNoTracking()
                .ToListAsync();

            var userIdList = userData.Select(u => u.Id).ToList();

            // 3. Фото (второй запрос)
            var photos = await _context.Photos
                .Where(p => userIdList.Contains(p.User.Id))
                .Select(p => new { p.Id, p.Path, UserId = p.User.Id })
                .AsNoTracking()
                .ToListAsync();

            // 4. Интересы (третий запрос)
            var interests = await _context.UserInterests
                .Where(ui => userIdList.Contains(ui.User.Id))
                .Select(ui => new { UserId = ui.User.Id, ui.Interest.Id, ui.Interest.Name })
                .AsNoTracking()
                .ToListAsync();

            // 5. Сборка
            var result = userData.Select(u => new
            {
                Id = u.Id,
                Name = u.Caption,
                City = u.CityName,
                Photos = photos.Where(p => p.UserId == u.Id).Select(p => new { p.Id, p.Path }).ToList(),
                Interests = interests.Where(i => i.UserId == u.Id).Select(i => new { i.Id, i.Name }).ToList()
            }).ToList();

            return Ok(result);
        }

        public class SearchFilters
        {
            public int AgeMin { get; set; } = 18;
            public int AgeMax { get; set; } = 80;
            public string? CityId { get; set; }
            public bool UseGeolocation { get; set; }
            public double? Latitude { get; set; }
            public double? Longitude { get; set; }
            public int RadiusKm { get; set; } = 50;
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 20;
        }

        [Authorize]
        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] SearchFilters filters)
        {
            try
            {
                var userId = UserId;
                var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);

                if (user == null)
                    return Unauthorized();

                // Вычисляем даты для фильтра по возрасту
                var today = DateOnly.FromDateTime(DateTime.Now);
                var minBirthDate = today.AddYears(-filters.AgeMax);
                var maxBirthDate = today.AddYears(-filters.AgeMin);

                // Коэффициент: градусы в км
                const double KmPerDegree = 111.0;

                // Пагинация
                var skip = (filters.Page - 1) * filters.PageSize;
                var take = filters.PageSize;

                // Если геолокация — считаем границы квадрата для грубого фильтра
                double? minLat = null, maxLat = null, minLon = null, maxLon = null;

                if (filters.UseGeolocation && filters.Latitude.HasValue && filters.Longitude.HasValue)
                {
                    var delta = filters.RadiusKm / KmPerDegree;
                    minLat = filters.Latitude.Value - delta;
                    maxLat = filters.Latitude.Value + delta;
                    minLon = filters.Longitude.Value - delta;
                    maxLon = filters.Longitude.Value + delta;
                }

                var query = _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id != userId
                                && u.Username != "admin"
                                && u.Birthday >= minBirthDate
                                && u.Birthday <= maxBirthDate
                                && u.Sex != user.Sex)
                    .Join(_context.Profiles,
                        u => u.Id,
                        p => p.UserId,
                        (u, p) => new { User = u, Profile = p })
                    .Join(_context.Cities,
                        up => up.Profile.CityId,
                        c => c.Id,
                        (up, c) => new
                        {
                            User = up.User,
                            Profile = up.Profile,
                            City = c
                        })
                    .AsQueryable();

                // Грубый гео-фильтр
                if (filters.UseGeolocation && minLat.HasValue)
                {
                    query = query.Where(x =>
                        x.City.Latitude >= minLat &&
                        x.City.Latitude <= maxLat &&
                        x.City.Longitude >= minLon &&
                        x.City.Longitude <= maxLon);
                }

                // Фильтр по городу (если не геолокация)
                if (!filters.UseGeolocation && !string.IsNullOrEmpty(filters.CityId))
                {
                    if (Guid.TryParse(filters.CityId, out var cityGuid))
                    {
                        query = query.Where(x => x.Profile.CityId == cityGuid);
                    }
                }

                // Подсчет общего количества для пагинации (опционально)
                var totalCount = await query.CountAsync();

                // Загружаем данные с пагинацией
                var data = await query
                    .OrderBy(x => x.User.Caption) // Можно изменить на нужный порядок
                    .Skip(skip)
                    .Take(take)
                    .Select(x => new
                    {
                        UserId = x.User.Id,
                        Name = x.User.Caption,
                        BirthDate = x.User.Birthday,
                        Age = today.Year - x.User.Birthday.Year -
                              (today.DayOfYear < x.User.Birthday.DayOfYear ? 1 : 0),
                        CityId = x.City.Id,
                        CityName = x.City.Name,
                        Lat = x.City.Latitude,
                        Lon = x.City.Longitude,
                        Distance = filters.UseGeolocation && filters.Latitude.HasValue && filters.Longitude.HasValue
                            //? (double)Math.Sqrt((double)Math.Pow((double)(x.City.Latitude - filters.Latitude.Value) * KmPerDegree, 2) +
                            //                    ((double)x.City.Longitude - filters.Longitude.Value) * KmPerDegree * 
                            //                    (double)Math.Cos(filters.Latitude.Value * Math.PI / 180)):0,

                            ? Math.Round(
                                (double)Math.Pow(((double)x.City.Latitude - filters.Latitude.Value) * KmPerDegree, 2) +
                                (double)Math.Pow(((double)x.City.Longitude - filters.Longitude.Value) * KmPerDegree *
                                                 (double)Math.Cos(filters.Latitude.Value * Math.PI / 180), 2), 0)
                            : (double?)null,
                        PhotoId = _context.PhotoMain
                            .Where(pm => pm.User.Id == x.User.Id)
                            .Select(pm => pm.PhotoId)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                // Преобразуем в формат для фронтенда
                var result = data.Select(x => new
                {
                    photoId = x.PhotoId != Guid.Empty ? x.PhotoId.ToString() : null,
                    userId = x.UserId,
                    name = x.Name,
                    age = x.Age,
                    cityId = x.CityId.ToString(),
                    distance = x.Distance.HasValue ? (double)Math.Round(Math.Sqrt(x.Distance.Value), 0) : (double?)null
                })
                .OrderBy(x => x.distance)
                .Distinct()
                .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка при поиске: " + ex.Message });
            }
        }

    }
}