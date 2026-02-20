using DeepSeek.ApiClient.Interfaces;
using DeepSeek.ApiClient.Models;
using LP.Entity;

namespace LP.Server.Services
{
    public class DialogAnalysis
    {
        public int Score { get; set; }              // Оценка диалога (1-10)
        public string Sentiment { get; set; }       // Тональность: positive/neutral/negative
        public List<string> RedFlags { get; set; }  // Красные флаги (опасные моменты)
        public List<string> Advice { get; set; }    // Советы по улучшению
    }
    public class AIAnalyzerService
    {
        private readonly IDeepSeekClient _deepSeekClient;

        public AIAnalyzerService(IDeepSeekClient deepSeekClient)
        {
            _deepSeekClient = deepSeekClient;
        }

        public async Task<DialogAnalysis> AnalyzeDialogAsync(List<Message> messages)
        {
            var dialogText = string.Join("\n",
                messages.Select(m => $"{m.ChatId}: {m.Text}"));

            var request = new DeepSeekRequestBuilder()
                .SetModel(DeepSeekModel.V3)
                .SetTemperature(0.2)
                .SetSystemMessage(@"
                    Ты — анализатор диалогов для сайта знакомств. 
                    ВСЕГДА возвращай ответ в строгом JSON формате. 
                    Никакого дополнительного текста, только JSON.

                    Схема JSON:
        {
            """"score"""": число от 1 до 10,
            """"compatibility"""": число от 0 до 100,
            """"tone"""": """"одно слово из: friendly, flirty, neutral, aggressive"""",
            """"interests"""": [""""интерес1"""", """"интерес2""""],
            """"warnings"""": [""""предупреждение1""""]
        }
                ")
                .AddUserMessage("Анализируй этот диалог: " + dialogText)
                .Build();

            var response = await _deepSeekClient.SendMessageAsync(request);
            return new DialogAnalysis
            {
                Score = 0,
                Sentiment = "Не удалось проанализировать диалог" 
            };
            //return JsonSerializer.Deserialize<DialogAnalysis>(response);
        }
    }
}
