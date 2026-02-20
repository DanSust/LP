using Azure;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LP.Server.Services
{
    public class OllamaRawResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; }  // Сырой JSON как строка

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }
    public class AnalysisResult  // Ваши данные из AI
    {
        public string Response { get; set; }
        public int Compatibility { get; set; }
        public string Tone { get; set; }
        public List<string> Warnings { get; set; }
        //public List<string> Advice { get; set; }
    }
    public class LocalAIService
    {
        private readonly HttpClient _httpClient;

        public LocalAIService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:11434");
        }

        public async Task<AnalysisResult> GenerateAsync(string prompt)
        {
            var request = new
            {
                model = "qwen2.5:7b",
                options = new
                {
                    temperature = 0.9,
                    seed = Random.Shared.Next(), // Случайный seed — разные ответы
                    repeat_penalty = 1.5,       // Штраф за повторы
                    num_predict = 100
                },
                prompt = @"
                    Проанализируй этот диалог и дай краткий комментарий. 
                    Без общих фраз. Только конкретика по этому разговору.

                    Схема JSON:
                    {{
                        """"Response"""": [""""ответ""""],
                        """"Compatibility"""": число от 0 до 100,
                        """"Tone"""": """"одно слово из: friendly, flirty, neutral, aggressive"""",            
                        """"Warnings"""": [""""предупреждение1""""]
                    }}

                    диалог: {prompt}",
                stream = false,
                format = "json"
            };

            // Шаг 1: Отправляем запрос и получаем HttpResponseMessage
            var response = await _httpClient.PostAsJsonAsync("/api/generate", request);
            response.EnsureSuccessStatusCode();

            // Шаг 2: Десериализуем тело ответа в OllamaRawResponse
            var raw = await response.Content.ReadFromJsonAsync<OllamaRawResponse>();

            // Шаг 3: Парсим JSON из строки response
            try
            {
                var result = JsonSerializer.Deserialize<AnalysisResult>(
                    raw.Response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
            
        }
    }
}
