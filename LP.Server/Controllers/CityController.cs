using LP.Entity;
using LP.Entity.Store;
using LP.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CityController : RedisController
    {
        private readonly ApplicationContext _context;
        private const string CITIES_CACHE_KEY = "cities:all";

        public CityController(ApplicationContext context, IDistributedCache cache) : base(cache) 
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            try
            {
                // Используем безопасный метод получения из кеша
                var cities = await GetFromCacheSafeAsync(
                    CITIES_CACHE_KEY,
                    async () => await _context.Cities.OrderBy(x => x.Name).ToListAsync(),
                    TimeSpan.FromHours(24)
                );

                return Ok(cities);
            }
            catch (Exception ex)
            {
                // Логируем ошибку и возвращаем данные напрямую из БД
                var cities = await _context.Cities.OrderBy(x => x.Name).ToListAsync();
                return Ok(cities);
            }
        }

        [AllowAnonymous]
        [HttpGet("nearest")]
        public async Task<IActionResult> FindNearest([FromQuery] double latitude, [FromQuery] double longitude)
        {
            var cities = await _context.Cities.ToListAsync();

            if (!cities.Any())
            {
                return NotFound("No cities available");
            }

            City nearestCity = null;
            double minDistance = double.MaxValue;

            foreach (var city in cities)
            {
                var distance = CalculateDistance(latitude, longitude, city.Latitude, city.Longitude);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestCity = city;
                }
            }

            if (nearestCity == null)
            {
                return NotFound("Could not find nearest city");
            }

            return Ok(new
            {
                city = nearestCity,
                distance = minDistance
            });
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Радиус Земли в км

            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double ToRad(double degrees)
        {
            return degrees * (Math.PI / 180);
        }
    }
}
