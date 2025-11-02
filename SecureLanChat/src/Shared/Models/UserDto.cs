using System.ComponentModel.DataAnnotations;

namespace SecureLanChat.Models
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

