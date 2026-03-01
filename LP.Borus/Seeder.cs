using Bogus;
using LP.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Borus
{
    public class Seeder
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationContext _context; // твой DbContext
        private readonly MD5 _md5 = MD5.Create();
        private List<Interest> _interests = new();
        private readonly HashSet<string> _usedEmails = new();

        // Метод для вычисления хеша
        private static byte[] ComputeHash(string password)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        public Seeder(ApplicationContext context)
        {
            _context = context;
            _httpClient = new HttpClient();
        }

        public async Task SeedUsersAsync(int count = 1000)
        {
            // Очистка перед заполнением (опционально)
            // await _context.Database.ExecuteSqlRawAsync("DELETE FROM Photos");
            // await _context.Database.ExecuteSqlRawAsync("DELETE FROM UserInterests");
            // await _context.Database.ExecuteSqlRawAsync("DELETE FROM Profiles");
            // await _context.Database.ExecuteSqlRawAsync("DELETE FROM Users");

            _interests = await _context.Interests.ToListAsync();
            var existingEmails = await _context.Users.Select(u => u.Email).ToListAsync();

            var faker = new Faker("ru"); // Русские данные для "Синхронного Сердцебиения"

            for (int i = 0; i < count; i++)
            {
                var sex = faker.Random.Bool();
                var firstName = sex ? faker.Name.FirstName(Bogus.DataSets.Name.Gender.Male)
                                    : faker.Name.FirstName(Bogus.DataSets.Name.Gender.Female);

                string email;
                int attempts = 0;
                do
                {
                    email = faker.Internet.Email(firstName);
                    if (attempts > 100) // Если застряли, добавляем случайные числа
                    {
                        email = $"{firstName.ToLower()}{faker.Random.Int(1000, 999999)}@{faker.Internet.DomainName()}";
                    }
                    attempts++;
                } while (_usedEmails.Contains(email.ToLower()));

                _usedEmails.Add(email.ToLower());

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Caption = firstName,
                    Username = faker.Internet.UserName(firstName),
                    Birthday = DateOnly.FromDateTime(faker.Date.Past(30, DateTime.Now.AddYears(-18))),
                    PasswordHash = _md5.ComputeHash(Encoding.ASCII.GetBytes("123123")),
                    Sex = sex,
                    Created = faker.Date.Past(2),
                    Email = email,
                    LastLogin = faker.Date.Recent(30),
                    IsPaused = faker.Random.Double() < 0.1 // 10% на паузе
                };

                // Создаем профиль
                var profile = new Profile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Description = faker.Rant.Review("dating"),
                    Weight = faker.Random.Int(45, 120),
                    Height = faker.Random.Int(150, 200),
                    AgeFrom = faker.Random.Int(18, 35),
                    AgeTo = faker.Random.Int(25, 65),
                    CityId = Guid.Parse("1EB8202D-BDAC-45CF-A855-1F66E300855E"),
                    Aim = faker.PickRandom<Aim>(),
                    WithPhoto = true
                };

                // Добавляем 1-5 фото из Picsum (бесплатный сервис случайных фото)
                var photoCount = faker.Random.Int(1, 1);
                var photos = new List<Photo>();

                for (int p = 0; p < photoCount; p++)
                {
                    var photoId = Guid.NewGuid();
                    //var photoUrl = $"https://picsum.photos/seed/{user.Id}-{p}/400/600";
                    var photoUrl = $"https://thispersondoesnotexist.com/?t={Guid.NewGuid()}";

                    // Скачиваем и сохраняем локально (опционально)
                    var localPath = await DownloadPhotoAsync(photoUrl, user.Id, photoId);

                    photos.Add(new Photo
                    {
                        Id = photoId,
                        User = user,
                        Path = localPath ?? photoUrl // или сохраняем URL
                    });
                }

                // Основное фото
                var mainPhoto = new PhotoMain
                {
                    PhotoId = photos.First().Id,
                    User = user
                };

                // Добавляем 3-7 интересов
                var userInterests = faker.PickRandom(_interests, faker.Random.Int(3, 7))
                    .Select((interest, idx) => new UserInterest
                    {
                        Id = Guid.NewGuid(),
                        User = user,
                        Interest = interest, // Теперь это сущность из базы
                        Order = idx
                    })
                    .ToList();

                _context.Users.Add(user);
                _context.Profiles.Add(profile);
                _context.Photos.AddRange(photos);
                _context.PhotoMain.Add(mainPhoto);
                _context.UserInterests.AddRange(userInterests);

                // Сохраняем пачками по 50 для производительности
                if (i % 50 == 0)
                {
                    try
                    {
                        await _context.SaveChangesAsync();
                    } catch  { }

                    Console.WriteLine($"Загружено {i}/{count} пользователей...");
                }
            }

            await _context.SaveChangesAsync();
            Console.WriteLine($"Готово! Добавлено {count} пользователей.");
        }

        private async Task<string?> DownloadPhotoAsync(string url, Guid userId, Guid photoId)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = $"{photoId}";
                var path = Path.Combine(@"d:\Work\LP\img", userId.ToString(), fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, bytes);

                return path;
            }
            catch
            {
                return null; // Если не удалось скачать, вернем null
            }
        }
    }
}
