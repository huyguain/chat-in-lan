using System.ComponentModel.DataAnnotations;

namespace SecureLanChat.Models
{
    public class User
    {
        public Guid Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
        
        [EmailAddress]
        public string? Email { get; set; }
        
        public string? PasswordHash { get; set; }
        
        [Required]
        [StringLength(2048)]
        public string PublicKey { get; set; } = string.Empty;
        
        public bool IsOnline { get; set; }
        
        public DateTime LastSeen { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<Message> SentMessages { get; set; } = new List<Message>();
        public virtual ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
        public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
    }

    public enum UserStatus
    {
        Offline = 0,
        Online = 1,
        Away = 2,
        Busy = 3
    }
}
