namespace LP.Chat.Interfaces
{
    public interface IMessageBotService
    {
        Task StartDialogAsync(Guid fromUserId, Guid toUserId);
        Task<bool> IsBotDialogActive(Guid fromUserId, Guid toUserId);
        Task ProcessUserResponse(Guid fromUserId, Guid toUserId, string responseText, Guid chatId);
        Task StopDialogAsync(Guid fromUserId, Guid toUserId);
        public void StopChat(Guid fromUserId, Guid toUserId);
    }
}
