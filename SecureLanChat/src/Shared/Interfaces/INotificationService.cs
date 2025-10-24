namespace SecureLanChat.Interfaces
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string userId, string message, NotificationType type);
        Task SendDesktopNotificationAsync(string title, string message);
        Task PlayNotificationSoundAsync();
        Task UpdateUnreadCountAsync(string userId, int count);
        Task<int> GetUnreadCountAsync(string userId);
    }

    public enum NotificationType
    {
        NewMessage,
        UserOnline,
        UserOffline,
        SystemMessage,
        Error
    }
}
