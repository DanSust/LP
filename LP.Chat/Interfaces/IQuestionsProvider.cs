namespace LP.Chat.Interfaces
{
    public interface IQuestionsProvider
    {
        Task<QuestionNode?> GetNextQuestionAsync(Guid questionerUserId, Guid answererUserId);
        Task<QuestionNode?> GetActiveQuestionAsync(Guid questionerUserId, Guid answererUserId);
        Task SubmitAnswerAsync(Guid questionId, Guid answererUserId, string answer);
        Task<bool> HasUnansweredQuestionsAsync(Guid questionerUserId, Guid answererUserId);
    }

    public class QuestionNode
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Question { get; set; } = string.Empty;
        public int Order { get; set; } = 0;
        public int TotalCount { get; set; } = 0;
        public bool Send { get; set; } = false;
        public bool Answered { get; set; } = false;
    }
}
