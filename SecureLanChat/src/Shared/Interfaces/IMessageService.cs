using SecureLanChat.Models;

namespace SecureLanChat.Interfaces
{
    public interface IMessageService
    {
        Task SendMessageAsync(string senderId, string receiverId, string content);
        Task SendBroadcastAsync(string senderId, string content);
        Task<List<Message>> GetMessageHistoryAsync(string userId, int page = 1, int pageSize = 50);
        Task<List<Message>> SearchMessagesAsync(string userId, string searchTerm);
        Task<Message?> GetMessageByIdAsync(string messageId);
        Task DeleteMessageAsync(string messageId);
    }
}
