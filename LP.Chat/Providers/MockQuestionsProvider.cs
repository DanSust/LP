using LP.Chat.Interfaces;
using System.Threading;

namespace LP.Chat.Providers
{
    public class MockQuestionsProvider : IQuestionsProvider
    {
        private readonly object _lock = new object();
        private const string SEPARATOR = "||";
        private readonly Dictionary<Guid, List<QuestionNode>> _questions = new()
        {
            [Guid.Parse("41A1B8A2-B70D-4ABE-9758-E0D86988DF4D")] = new()
            {
                new() { Id = Guid.NewGuid(), Question = "Какое ваше любимое время года?", Order = 1, TotalCount = 3 },
                new() { Id = Guid.NewGuid(), Question = "Где вы видите себя через 5 лет?", Order = 2, TotalCount = 3 },
                new() { Id = Guid.NewGuid(), Question = "Что для вас значит семья?", Order = 3, TotalCount = 3 }
            },
            [Guid.Parse("304b7203-32cc-477d-b07b-6c68d4f3d957")] = new() // Вопросы Даниила
            {
                new() { Id = Guid.NewGuid(), Question = "Какой ваш любимый фильм?", Order = 1, TotalCount = 2 },
                new() { Id = Guid.NewGuid(), Question = "Что вы цените в людях?", Order = 2, TotalCount = 2 }
            }
        };

        private readonly HashSet<string> _answered = new();

        // key: $"{answererId}-{questionerId}" - последний отправленный вопрос
        private readonly Dictionary<string, Guid> _lastSent = new();

        public Task<QuestionNode?> GetNextQuestionAsync(Guid questioner, Guid answerer)
        {
            lock (_lock) // <-- Добавить
            {
                if (!_questions.TryGetValue(questioner, out var qs))
                    return Task.FromResult<QuestionNode?>(null);

                var sentKey = $"{answerer}{SEPARATOR}{questioner}";

                // Если уже есть отправленный вопрос - не отправляем новый
                if (_lastSent.ContainsKey(sentKey))
                    return Task.FromResult<QuestionNode?>(null);

                _lastSent.Add(sentKey, answerer);
                // Находим первый неотвеченный
                var answeredIds = _answered
                    .Where(a => a.StartsWith($"{answerer}{SEPARATOR}{questioner}{SEPARATOR}"))
                    .Select(a => Guid.Parse(a.Split(new[] {SEPARATOR}, StringSplitOptions.None)[2]))
                    .ToHashSet();

                var next = qs.FirstOrDefault(q => !answeredIds.Contains(q.Id));
                return Task.FromResult(next);
            }
        }

        public Task<QuestionNode?> GetActiveQuestionAsync(Guid questioner, Guid answerer)
        {
            var sentKey = $"{answerer}{SEPARATOR}{questioner}";
            if (_lastSent.TryGetValue(sentKey, out var questionId))
            {
                var allQuestions = _questions.Values.SelectMany(list => list);
                return Task.FromResult(allQuestions.FirstOrDefault(q => q.Id == questionId));
            }
            return Task.FromResult<QuestionNode?>(null);
        }

        public Task SubmitAnswerAsync(Guid qId, Guid answerer, string answer)
        {
            lock (_lock)
            {
                var questioner = FindQuestionerByQuestionId(qId);
                if (questioner.HasValue)
                {
                    _answered.Add($"{answerer}{SEPARATOR}{questioner.Value}{SEPARATOR}{qId}");
                    _lastSent.Remove($"{answerer}{SEPARATOR}{questioner.Value}");
                }

                return Task.CompletedTask;
            }
        }

        private Guid? FindQuestionerByQuestionId(Guid questionId)
        {
            foreach (var kvp in _questions)
            {
                if (kvp.Value.Any(q => q.Id == questionId))
                    return kvp.Key;
            }
            return null;
        }

        public Task<bool> HasUnansweredQuestionsAsync(Guid questioner, Guid answerer)
        {
            var sentKey = $"{answerer}{SEPARATOR}{questioner}";

            // Если есть активный вопрос
            if (_lastSent.ContainsKey(sentKey))
                return Task.FromResult(true);

            // Или есть следующий вопрос
            return GetNextQuestionAsync(questioner, answerer)
                .ContinueWith(t => t.Result != null);
        }
    }
}

