using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LP.Entity;
public interface ICityLoader
{
    Task<int> LoadFromTextFileAsync(string filePath);
}

public class NominatimResult
{
    [JsonPropertyName("lat")]
    public string Lat { get; set; } 
    [JsonPropertyName("lon")]
    public string Lon { get; set; } 
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class CityLoader : ICityLoader
{
    private readonly ApplicationContext _ctx;
    private readonly HttpClient _httpClient;

    public CityLoader(ApplicationContext ctx)
    {
        _ctx = ctx;
        _httpClient = new HttpClient
        {
            // Обязательно: User-Agent для Nominatim
            DefaultRequestHeaders = { { "User-Agent", "LP.App/1.0" } }
        };
    }

    public async Task<int> LoadFromTextFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("City file not found", filePath);

        int inserted = 0;
        int updated = 0;

        using var reader = new StreamReader(filePath, Encoding.UTF8);

        string? line;
        int lineNo = 0;

        var existing = await _ctx.Cities
            .AsNoTracking()
            .Select(c => new
            {
                Name = c.Name,
                Latitude = c.Latitude,
                Longitude = c.Longitude
            })
            .ToArrayAsync();

        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNo++;

            var cityName = line.Trim();
            if (string.IsNullOrWhiteSpace(cityName)) continue;

            var existingCity = existing.FirstOrDefault(c => c.Name == cityName);

            // Пропускаем если город есть И с координатами (не 0)
            if (existingCity != null && existingCity.Latitude != 0 && existingCity.Longitude != 0)
                continue;

            // Получаем координаты из Nominatim
            var (lat, lon) = await GetCoordinatesAsync(cityName);

            // Если координаты не получены — пропускаем или сохраняем с 0,0
            if (!lat.HasValue || !lon.HasValue)
            {
                // Можно пропустить: continue;
                // Или сохранить с 0,0 для повторной обработки
            }

            if (existingCity != null)
            {
                // ОБНОВЛЯЕМ существующий город (был без координат)
                var cityToUpdate = await _ctx.Cities.FirstAsync(c => c.Name == cityName);
                cityToUpdate.Latitude = (double)(lat ?? 0);
                cityToUpdate.Longitude = (double)(lon ?? 0);
                _ctx.Cities.Update(cityToUpdate);
                updated++;
            }
            else
            {
                // ДОБАВЛЯЕМ новый город
                var city = new City
                {
                    Id = Guid.NewGuid(),
                    Name = cityName,
                    Latitude = (double)(lat ?? 0),
                    Longitude = (double)(lon ?? 0)
                };
                _ctx.Cities.Add(city);
                inserted++;
            }

            // Пауза для rate limit Nominatim (1 запрос/сек)
            await Task.Delay(1000);
        }

        await _ctx.SaveChangesAsync();
        
        return inserted + updated;
    }

    private async Task<(decimal? Latitude, decimal? Longitude)> GetCoordinatesAsync(string cityName)
    {
        try
        {
            // Добавляем "Russia" для уточнения, можно параметризовать
            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(cityName)},Russia&format=json&limit=1";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return (null, null);
            }

            var json = await response.Content.ReadAsStringAsync();
            var results = JsonSerializer.Deserialize<NominatimResult[]>(json);

            if (results?.Length > 0)
            {
                var first = results[0];
                {
                    return (decimal.Parse(first.Lat), decimal.Parse(first.Lon));
                }
            }
            
            return (null, null);
        }
        catch (Exception ex)
        {
            return (null, null);
        }
    }
}