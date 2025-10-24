using System.ComponentModel.DataAnnotations;

namespace SecureLanChat.Models
{
    public class Session
    {
        public Guid Id { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string ConnectionId { get; set; } = string.Empty;
        
        [Required]
        [StringLength(256)]
        public string AESKey { get; set; } = string.Empty; // Encrypted AES key
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime ExpiresAt { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}
