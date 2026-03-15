using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public abstract class RedisController : BaseAuthController
    {
        protected readonly IDistributedCache _cache;
        private bool? _redisAvailable;
        private readonly SemaphoreSlim _redisCheckLock = new SemaphoreSlim(1, 1);
        private DateTime? _lastCheckTime;

        protected RedisController(IDistributedCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Проверяет доступность Redis с кэшированием результата на 30 секунд
        /// </summary>
        protected async Task<bool> IsRedisAvailableAsync(bool forceRefresh = false)
        {
            // Если проверяли менее 30 секунд назад и не нужно принудительное обновление
            if (!forceRefresh && _lastCheckTime.HasValue &&
                DateTime.UtcNow - _lastCheckTime.Value < TimeSpan.FromSeconds(30) &&
                _redisAvailable.HasValue)
            {
                return _redisAvailable.Value;
            }

            await _redisCheckLock.WaitAsync();
            try
            {
                // Повторная проверка после получения блокировки
                if (!forceRefresh && _lastCheckTime.HasValue &&
                    DateTime.UtcNow - _lastCheckTime.Value < TimeSpan.FromSeconds(30) &&
                    _redisAvailable.HasValue)
                {
                    return _redisAvailable.Value;
                }

                var testKey = $"health:test:{Guid.NewGuid():N}";

                // Пытаемся записать и прочитать тестовое значение с таймаутом
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                await _cache.SetStringAsync(
                    testKey,
                    "ok",
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(3)
                    },
                    cts.Token
                );

                var result = await _cache.GetStringAsync(testKey, cts.Token);
                _redisAvailable = result == "ok";

                // Очищаем тестовый ключ
                if (_redisAvailable.Value)
                {
                    await _cache.RemoveAsync(testKey, cts.Token);
                }

                _lastCheckTime = DateTime.UtcNow;

                return _redisAvailable.Value;
            }
            catch (OperationCanceledException)
            {
                _redisAvailable = false;
                _lastCheckTime = DateTime.UtcNow;
                return false;
            }
            catch (Exception ex)
            {
                _redisAvailable = false;
                _lastCheckTime = DateTime.UtcNow;
                return false;
            }
            finally
            {
                _redisCheckLock.Release();
            }
        }

        /// <summary>
        /// Безопасно получает значение из кеша с проверкой доступности Redis
        /// </summary>
        protected async Task<T> GetFromCacheSafeAsync<T>(string key, Func<Task<T>> getFromDbFunc, TimeSpan? expiration = null)
        {
            // 1. Пробуем получить из кеша БЕЗ предварительных проверок "доступности"
            try
            {
                // В нормальных библиотеках (StackExchange.Redis) GetStringAsync сам упадет по таймауту, 
                // если Redis недоступен. Это быстрее, чем отдельный Ping.
                var cached = await _cache.GetStringAsync(key);
                if (!string.IsNullOrEmpty(cached))
                {
                    return JsonSerializer.Deserialize<T>(cached);
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку ОДИН раз, чтобы знать, что Redis упал
                // Log.Error(ex, "Redis cache read error for key {Key}", key);
                Console.WriteLine("Redis cache read error");
            }

            // 2. Идем в БД (это наше основное действие, если кеш подвел)
            var result = await getFromDbFunc();

            // 3. Сохраняем в кеш "в фоновом режиме" (fire and forget или просто в try-catch)
            if (result != null)
            {
                try
                {
                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromHours(24)
                    };
                    await _cache.SetStringAsync(key, JsonSerializer.Serialize(result), options);
                }
                catch
                {
                    Console.WriteLine("Redis cache read error");
                }
            }

            return result;
        }

        /// <summary>
        /// Безопасно удаляет значение из кеша
        /// </summary>
        protected async Task RemoveFromCacheSafeAsync(string key)
        {
            if (await IsRedisAvailableAsync())
            {
                try
                {
                    await _cache.RemoveAsync(key);
                    
                }
                catch (Exception ex)
                {
                    
                }
            }
        }

        /// <summary>
        /// Простая проверка доступности Redis (без кэширования результата)
        /// </summary>
        protected async Task<bool> PingRedisAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _cache.GetStringAsync("ping", cts.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}