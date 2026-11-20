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
        private readonly IConfiguration _configuration;

        public VotesController(ApplicationContext context, IConfiguration configuration) : base(context)
        {
            _configuration = configuration;
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

            // Сообщаем клиенту, что лайк стал взаимным: он покажет splash
            // «У вас есть пара» с кнопкой перехода в чат.
            var isMutual = await _context.Votes
                .AsNoTracking()
                .AnyAsync(v =>
                    v.Owner == id &&
                    v.Like == UserId &&
                    v.IsLike &&
                    !v.IsReject);

            // Диалог считается начатым сразу после создания чата —
            // даже если в нём ещё нет ни одного сообщения.
            var hasChat = await _context.Chats
                .AsNoTracking()
                .AnyAsync(c =>
                    (c.Owner == UserId && c.UserId == id) ||
                    (c.Owner == id && c.UserId == UserId));

            // Чат уже создан — splash «У вас есть пара» не нужен, даже если лайки взаимные.
            return Ok(new { message = "OK", isMutual = isMutual && !hasChat });
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

        private class MatchDto
        {
            public Guid UserId { get; set; }
            public Guid MatchedUserId { get; set; }
            public string MatchType { get; set; } = null!;
            public DateTime? LikeDate { get; set; }
        }

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
            //var sql = "SELECT * FROM dbo.GetUserMatches({0}, {1}) ORDER BY LastAdded DESC";

            //var allMatches = await _context.MatchResults
            //    .FromSqlRaw(sql, userId, category ?? (object)DBNull.Value)
            //    .AsNoTracking()
            //    .ToListAsync();
            List<MatchDto> allMatches = null;
            try
            {
                allMatches = await _context.Database
                    .SqlQueryRaw<MatchDto>("SELECT * FROM dbo.GetUserMatches({0}, {1}) ORDER BY LikeDate DESC", userId, category)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка при поиске: " + ex.Message });
            }

            // Заблокированные и отмеченные как спам не должны попадать в мэтчи.
            var hiddenMatchIds = (await _context.HiddenUserIds(userId).ToListAsync()).ToHashSet();
            if (hiddenMatchIds.Count > 0)
            {
                allMatches = allMatches
                    .Where(m => !hiddenMatchIds.Contains(m.UserId))
                    .ToList();
            }

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
                    category = m.MatchType,
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

        //[Authorize]
        [HttpGet("list")]
        public async Task<IActionResult> GetVoteList(int count = 5)
        {
            count = Math.Clamp(count, 1, 20);

            var userId = UserId;
            var user = userId == Guid.Empty
                ? null
                : await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);

            // ---- Режим без авторизации ----
            if (user == null)
            {
                var queryN = _context.Users
                    .AsNoTracking()
                    .Where(x => x.Username != "admin");

                var totalN = await queryN.CountAsync();
                if (totalN == 0)
                    return Ok(new List<object>());

                var offsetN = totalN <= count ? 0 : Random.Shared.Next(0, totalN - count + 1);
                var randomIdsN = await queryN
                    .OrderBy(u => u.Id)
                    .Skip(offsetN)
                    .Take(count)
                    .Select(x => x.Id)
                    .ToListAsync();

                if (randomIdsN.Count == 0)
                    return Ok(new List<object>());

                var userDataN = await _context.Users
                    .AsNoTracking()
                    .Where(u => randomIdsN.Contains(u.Id))
                    .Select(u => new { u.Id, u.Caption, u.Sex, u.Birthday })
                    .ToListAsync();

                var profilesN = await _context.Profiles
                    .AsNoTracking()
                    .Where(p => randomIdsN.Contains(p.UserId))
                    .Select(p => new { p.UserId, p.CityId, p.Height, p.Weight, p.Description })
                    .ToListAsync();

                var profileByUserN = profilesN
                    .GroupBy(p => p.UserId)
                    .ToDictionary(g => g.Key, g => g.First());

                var cityIdsN = profileByUserN.Values
                    .Select(p => p.CityId)
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList();

                var citiesN = cityIdsN.Count == 0
                    ? new Dictionary<Guid, string>()
                    : await _context.Cities
                        .AsNoTracking()
                        .Where(c => cityIdsN.Contains(c.Id))
                        .Select(c => new { c.Id, c.Name })
                        .ToDictionaryAsync(c => c.Id, c => c.Name);

                var photosN = await _context.Photos
                    .AsNoTracking()
                    .Where(p => randomIdsN.Contains(p.User.Id))
                    .Select(p => new { p.Id, p.Path, UserId = p.User.Id })
                    .ToListAsync();

                var interestsN = await _context.UserInterests
                    .AsNoTracking()
                    .Where(ui => randomIdsN.Contains(ui.User.Id))
                    .Select(ui => new { UserId = ui.User.Id, ui.Interest.Id, ui.Interest.Name, ui.Interest.Path })
                    .ToListAsync();

                var resultN = userDataN.Select(u =>
                {
                    profileByUserN.TryGetValue(u.Id, out var profile);
                    string? city = null;
                    if (profile != null && profile.CityId != Guid.Empty)
                    {
                        citiesN.TryGetValue(profile.CityId, out city);
                    }

                    return new
                    {
                        Id = u.Id,
                        Name = u.Caption,
                        Sex = u.Sex,
                        City = city,
                        Birthday = u.Birthday,
                        Height = profile?.Height ?? 0,
                        Weight = profile?.Weight ?? 0,
                        Description = profile?.Description ?? string.Empty,
                        Photos = photosN.Where(p => p.UserId == u.Id).Select(p => new { p.Id, p.Path }).ToList(),
                        Interests = interestsN.Where(i => i.UserId == u.Id).Select(i => new { i.Id, i.Name, i.Path }).ToList()
                    };
                }).ToList();

                return Ok(resultN);
            }

            var currentUserProfile = await _context.Profiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var hiddenIds = await _context.HiddenUserIds(userId).ToListAsync();

            IQueryable<User> query = _context.Users
                .AsNoTracking()
                .Where(x => x.Username != "admin"
                            && x.Id != userId
                            && !hiddenIds.Contains(x.Id)
                            && x.IsPaused == false
                            && x.Sex != user.Sex
                            && !_context.Votes.Any(v => v.Owner == userId && v.Like == x.Id));

            if (currentUserProfile?.WithPhoto == true)
            {
                query = query.Where(x => _context.Photos.Any(p => p.User.Id == x.Id));
            }

            var total = await query.CountAsync();
            if (total == 0)
                return Ok(new List<object>());

            var offset = total <= count ? 0 : Random.Shared.Next(0, total - count + 1);
            var randomIds = await query
                .OrderBy(u => u.Id)
                .Skip(offset)
                .Take(count)
                .Select(x => x.Id)
                .ToListAsync();

            if (randomIds.Count == 0)
                return Ok(new List<object>());

            var userData = await _context.Users
                .AsNoTracking()
                .Where(u => randomIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Caption, u.Sex, u.Birthday })
                .ToListAsync();

            var profiles = await _context.Profiles
                .AsNoTracking()
                .Where(p => randomIds.Contains(p.UserId))
                .Select(p => new { p.UserId, p.CityId, p.Height, p.Weight, p.Description })
                .ToListAsync();

            var profileByUser = profiles
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            var cityIds = profileByUser.Values
                .Select(p => p.CityId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var cities = cityIds.Count == 0
                ? new Dictionary<Guid, string>()
                : await _context.Cities
                    .AsNoTracking()
                    .Where(c => cityIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name })
                    .ToDictionaryAsync(c => c.Id, c => c.Name);

            var photos = await _context.Photos
                .AsNoTracking()
                .Where(p => randomIds.Contains(p.User.Id))
                .Select(p => new { p.Id, p.Path, UserId = p.User.Id })
                .ToListAsync();

            var interests = await _context.UserInterests
                .AsNoTracking()
                .Where(ui => randomIds.Contains(ui.User.Id))
                .Select(ui => new { UserId = ui.User.Id, ui.Interest.Id, ui.Interest.Name, ui.Interest.Path })
                .ToListAsync();

            var result = userData.Select(u =>
            {
                profileByUser.TryGetValue(u.Id, out var profile);
                string? city = null;
                if (profile != null && profile.CityId != Guid.Empty)
                {
                    cities.TryGetValue(profile.CityId, out city);
                }

                return new
                {
                    Id = u.Id,
                    Name = u.Caption,
                    Sex = u.Sex,
                    City = city,
                    Birthday = u.Birthday,
                    Height = profile?.Height ?? 0,
                    Weight = profile?.Weight ?? 0,
                    Description = profile?.Description ?? string.Empty,
                    Photos = photos.Where(p => p.UserId == u.Id).Select(p => new { p.Id, p.Path }).ToList(),
                    Interests = interests.Where(i => i.UserId == u.Id).Select(i => new { i.Id, i.Name, i.Path }).ToList()
                };
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
            public Guid SortGuid { get; set; }
        }

        [Authorize]
        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] SearchFilters filters)
        {
            try
            {
                var userId = UserId;
                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);

                if (user == null)
                    return Unauthorized();

                // Скрываем заблокированных и тех, на кого пожаловались как на спам.
                var hiddenIds = await _context.HiddenUserIds(userId).ToListAsync();

                // Вычисляем даты для фильтра по возрасту
                var today = DateOnly.FromDateTime(DateTime.Now);
                var minBirthDate = today.AddYears(-filters.AgeMax);
                var maxBirthDate = today.AddYears(-filters.AgeMin);

                // Коэффициент: градусы в км
                const double KmPerDegree = 111.0;

                // Пагинация
                var maxResults = _configuration.GetValue<int>("Search:MaxResults", 100);
                if (maxResults <= 0) maxResults = 100;

                var page = filters.Page < 1 ? 1 : filters.Page;
                var pageSize = filters.PageSize < 1 ? 1 : filters.PageSize;

                var skip = (page - 1) * pageSize;
                var remaining = maxResults - skip;
                if (remaining <= 0)
                {
                    return Ok(Array.Empty<object>());
                }

                var take = Math.Min(pageSize, remaining);

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
                                && !hiddenIds.Contains(u.Id)
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
                var totalCount = Math.Min(await query.CountAsync(), maxResults);

                // Загружаем данные с пагинацией
                var data = await query
                    .OrderBy(x => filters.SortGuid) // Можно изменить на нужный порядок
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
                            .FirstOrDefault(),
                        FirstPhotoId = _context.Photos
                            .Where(p => p.User.Id == x.User.Id)
                            .Select(p => p.Id)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                // Преобразуем в формат для фронтенда
                var result = data.Select(x => new
                {
                    photoId = x.PhotoId != Guid.Empty ? x.PhotoId.ToString() : null,
                    firstPhotoId = x.FirstPhotoId != Guid.Empty ? x.FirstPhotoId.ToString() : null,
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
