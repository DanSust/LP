using LP.Common.Interfaces;
using LP.Entity;
//using LP.Entity.Migrations;
using LP.Entity.Store;
using LP.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Host;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LP.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatsController : BaseAuthController
    {
        private readonly ApplicationContext _context;
        private readonly ILikeRestrictionService _likeService;
        private readonly LocalAIService _analyzerService;
        public ChatsController(
            ApplicationContext context,
            ILikeRestrictionService likeService,
            LocalAIService analyzerService
            )
        {
            _context = context;
            _likeService = likeService;
            _analyzerService = analyzerService;
        }

        [Authorize]
        [HttpPost("get-or-create/{Id}")]
        public async Task<IActionResult> GetOrCreateChat(Guid Id)
        {
            var currentUserId = UserId;

            // Проверяем ограничение WithLikes через сервис
            var canChat = await _likeService.CanSendMessageAsync(currentUserId, Id);
            if (!canChat)
            {
                var status = await _likeService.GetLikeStatusAsync(currentUserId, Id);

                return BadRequest(new
                {
                    error = "Для начала переписки нужен взаимный лайк",
                    code = "MUTUAL_LIKE_REQUIRED",
                    iLiked = status.ILiked,
                    theyLiked = status.TheyLiked,
                    requiresMutualLike = true
                });
            }

            // Ищем существующий чат между текущим пользователем и целевым
            var existingChat = await _context.Chats
                .FirstOrDefaultAsync(c =>
                    (c.Owner == currentUserId && c.UserId == Id) ||
                    (c.Owner == Id && c.UserId == currentUserId));

            if (existingChat != null)
            {
                return Ok(new { chatId = existingChat.Id });
            }

            // Создаем новый чат
            var newChat = new Chat
            {
                Id = Guid.NewGuid(),
                Owner = Id,
                UserId = currentUserId,
                Time = DateTime.Now,
                Name = $"chat_{Guid.NewGuid()}" // Можно заменить на имя собеседника
            };

            _context.Chats.Add(newChat);
            await _context.SaveChangesAsync();

            return Ok(new { chatId = newChat.Id, owner = newChat.Owner, userId = newChat.UserId});
        }

        [Authorize]
        [HttpGet("delete/{Id}")]
        public async Task<IActionResult> DeleteChat(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var chat = await _context.Chats
                    .FirstOrDefaultAsync(c => c.Id == id);
                if (chat == null)
                    return NotFound(new { error = "Chat not found" });

                await _context.Messages.Where(x => x.ChatId == id).ExecuteDeleteAsync();
                _context.Chats.Remove(chat);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new {chatId = id, owner = chat.Owner, userId = chat.UserId});
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, new {error = "Failed to delete chat"});
            }
        }

        [Authorize]
        [HttpGet("list")]
        public async Task<IActionResult> List()
        {
            //return Ok(await _context.Chats.Where(x=>x.Owner == UserId || x.UserId == UserId).OrderByDescending(x => x.Time).ToListAsync());
            return Ok(await _context.Chats
                .Where(chat => chat.Owner == UserId || chat.UserId == UserId)
                .OrderByDescending(chat => chat.Time)
                .Select(chat => new
                {
                    chat.Id,
                    Name = _context.Users
                        .Where(u => u.Id == (chat.Owner == UserId ? chat.UserId : chat.Owner))
                        .Select(u => u.Caption)
                        .FirstOrDefault() ?? "Неизвестный",
                    chat.Owner,
                    UserId = chat.Owner == UserId ? chat.UserId : chat.Owner,
                    chat.Time,
                    UnreadMessagesCount = _context.Messages.Count(message =>
                        message.ChatId == chat.Id &&
                        message.UserId != UserId &&
                        message.Status == "delivered")
                })
                .ToListAsync());
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            return Ok(await _context.Messages
                .Where(x => x.ChatId == id)
                .OrderBy(x => x.Time)
                .Select(msg => new
                {
                    chatId = id,
                    id = msg.Id,
                    status = msg.Status,
                    text = msg.Text,
                    time = msg.Time,
                    userId = msg.UserId,
                    own = msg.UserId == UserId
                })
                .ToListAsync()
            );
        }

        [Authorize]
        [HttpPost("like/{id}")]
        public async Task<IActionResult> Like(Guid id)
        {
            var chat = _context.Chats.FindAsync(id).Result;

            if (chat == null)
            {
                _context.Chats.Add(new Chat() { Id = id, Owner = UserId, Time = DateTime.Now, Name = "chat_" + id.ToString()});
            }

            await _context.SaveChangesAsync();

            return Ok(id);
        }

        [Authorize]
        [HttpGet("questions/{userId}")]
        public async Task<IActionResult> GetQuestions(Guid userId) =>
            Ok(await _context.UserQuestions.Where(q => q.User.Id == userId).OrderBy(x=>x.Order).ToListAsync());

        [Authorize]
        [HttpPost("ai/{id}")]
        public async Task<IActionResult> AI(Guid id)
        {
            // Один запрос: сообщения + username через EF Core projection
            var messages = await _context.Messages
                .AsNoTracking()
                .Where(x => x.ChatId == id)
                .OrderBy(x => x.Time)
                .Select(m => new
                {
                    m.Text,
                    m.Time,
                    Username = _context.Users
                        .Where(u => u.Id == m.UserId)
                        .Select(u => u.Caption)
                        .FirstOrDefault() ?? "Unknown"
                })
                .ToListAsync();

            var text = string.Join("\n", messages.Select(m => $"{m.Username}: {m.Text}"));
            text = @"
28.10.2025, 20:07 - Татьяна LP: Не идеализирую)) все что связано с техникой и автоматизацией я пользователь так себе)) поэтому меня восхищает подобные умения
28.10.2025, 20:08 - Dan Sust: 👏
28.10.2025, 21:55 - Татьяна LP: Сегодня чудный вечер, совсем нет ветра, очень приятно
28.10.2025, 21:55 - Татьяна LP: Или в центре так
28.10.2025, 22:01 - Dan Sust: Я на хоккей ходил. Там тоже тепло 😂
28.10.2025, 22:02 - Dan Sust: Но вечером действительно хорошо
28.10.2025, 22:02 - Татьяна LP: Смотреть?
28.10.2025, 22:03 - Dan Sust: Да. Брат пригласил. В общем, наши, все полимеры просрали
28.10.2025, 22:05 - Татьяна LP: <Без медиафайлов>
28.10.2025, 22:11 - Татьяна LP: <Без медиафайлов>
28.10.2025, 22:11 - Dan Sust: «Пользователь установил ограничение на получение голосовых сообщений. Сообщение не доставлено».
28.10.2025, 22:16 - Татьяна LP: Ой а я не знала, что так можно
28.10.2025, 22:16 - Татьяна LP: Отправила тебе голосовое, так как за рулём еду
28.10.2025, 22:17 - Dan Sust: Я тоже был в Барселоне, тогда играл Зенит. Еще Халк тогда играл.
28.10.2025, 22:17 - Dan Sust: Случайно попали
28.10.2025, 22:17 - Dan Sust: Это шутка. А музыка на фоне понравилась
28.10.2025, 22:20 - Татьяна LP: <Без медиафайлов>
28.10.2025, 22:22 - Татьяна LP: <Без медиафайлов>
28.10.2025, 22:24 - Dan Sust: Я просто ездил в Португалию. Случайно совпало. Никогда не был ни чьим фанатом, болельщиков или сочувствующим
28.10.2025, 22:26 - Dan Sust: Мне скота кормить надо. Да и сам голодный
28.10.2025, 22:31 - Татьяна LP: Давай сооружать ужин 🤗
28.10.2025, 22:31 - Татьяна LP: А какой у тебя скот?
28.10.2025, 22:32 - Dan Sust: Мейкун
28.10.2025, 22:57 - Татьяна LP: Оооооо! Супер!
28.10.2025, 22:57 - Татьяна LP: Можно фото в студию 👏
28.10.2025, 23:00 - Dan Sust: Не уверен... Жрёт как свинья, гадит как взрослые, орёт как ребёнок, урчит как паравоз
28.10.2025, 23:01 - Dan Sust: <Без медиафайлов>
28.10.2025, 23:01 - Dan Sust: <Без медиафайлов>
28.10.2025, 23:10 - Dan Sust: Предвосхищу вопрос - его зовут Харя
28.10.2025, 23:16 - Татьяна LP: Он красивый
28.10.2025, 23:16 - Татьяна LP: И в нем виден борец
28.10.2025, 23:16 - Татьяна LP: Борец с тобой чтоли!
28.10.2025, 23:16 - Dan Sust: Думаю - он разумное ссыкло
28.10.2025, 23:17 - Татьяна LP: Вы соперничаете)
28.10.2025, 23:17 - Татьяна LP: За главенство самца 🫣
28.10.2025, 23:18 - Татьяна LP: Я люблю чёрных котов
28.10.2025, 23:18 - Dan Sust: У нас нет общей кошки, так, что мы спокойны
28.10.2025, 23:18 - Татьяна LP: 😂
28.10.2025, 23:18 - Татьяна LP: У вас общая территория 🤗
28.10.2025, 23:21 - Dan Sust: Тут даже ответить нечего. Пусть он так считает )
28.10.2025, 23:22 - Татьяна LP: <Без медиафайлов>
28.10.2025, 23:23 - Татьяна LP: Он тоже был крупный кг 7 и длинный такой мощный мужчина)
28.10.2025, 23:23 - Татьяна LP: Он так любил спать как человек на спине
28.10.2025, 23:23 - Dan Sust: Этот тоже 7. Сейчас наверное похудел немного
28.10.2025, 23:25 - Татьяна LP: Голубоглазый. Он не породный но выглядел очень эффектно видимо в роду имел ориенталов
28.10.2025, 23:26 - Татьяна LP: А сейчас у дочки живет бурма, бешеный. Мне такие не нравятся. Кот должен есть и лежать, как красивое дополнение интерьеру
28.10.2025, 23:28 - Dan Sust: А - знаю таких. У брата жены такой. Можно орать, пинать, через 15 сек снова забывает
28.10.2025, 23:29 - Татьяна LP: А ты чтоли такой агрессивный?
28.10.2025, 23:30 - Dan Sust: Конечно нет. Это он его ""воспитывает""
28.10.2025, 23:31 - Татьяна LP: Фух) а то я мне порой кажется что ты довольно жестокий
28.10.2025, 23:32 - Татьяна LP: Высказывания такие знаешь увереные, однозначные 😊
28.10.2025, 23:32 - Dan Sust: Это из-за краткости ответов. А виртуальное общение не выражает эмоции и не передает интонацию ))
28.10.2025, 23:33 - Татьяна LP: Может быть может быть
28.10.2025, 23:33 - Татьяна LP: Я вот пишу в основном без пунктуации… Потом сама читаю пипец ниче не понятно😅
28.10.2025, 23:36 - Татьяна LP: У меня вообще честно говоря есть особенность, о которой я поняла после сайта кстати) я не отличаюсь тактичностью, могу что прямолинейно сказануть, чем обижаю, хотя в своей голове совершенно не преследую мысль обидеть
28.10.2025, 23:37 - Татьяна LP: Аватарка зачетная)
28.10.2025, 23:37 - Татьяна LP: Прям сюрпрайз меня ждёт в четверг)
28.10.2025, 23:38 - Dan Sust: Думаю, это нормально. Назовем - это профдеформацией от такого общения
28.10.2025, 23:39 - Татьяна LP: Ну ты конечно подколол) я уж прям не считаю себя профессиональной ищущей 😅
28.10.2025, 23:39 - Татьяна LP: Язва видимо ещё тот)
28.10.2025, 23:39 - Dan Sust: Циник
28.10.2025, 23:40 - Татьяна LP: Сарказм так сказать
28.10.2025, 23:40 - Татьяна LP: Ну да)
28.10.2025, 23:40 - Татьяна LP: Смотря в какой степени, лёгкий цинизм забавен и привлекателен
28.10.2025, 23:41 - Dan Sust: Я не буду фрапировать вас. Так, что без сюрпризов ))
28.10.2025, 23:41 - Татьяна LP: Таааааак пошла читать новый термин…
28.10.2025, 23:42 - Татьяна LP: Фрапировать-быстро охладить блюдо перед подачей <Сообщение изменено>
28.10.2025, 23:43 - Dan Sust: Может есть еще определения? ))))
";

            var result = await _analyzerService.GenerateAsync(text);

            return Ok(result);
        }

    }
}