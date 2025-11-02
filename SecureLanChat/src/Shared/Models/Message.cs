using System.ComponentModel.DataAnnotations;

namespace SecureLanChat.Models
{
    public class Message
    {
        public Guid Id { get; set; }
        
        [Required]
        public Guid SenderId { get; set; }
        
        public Guid? ReceiverId { get; set; } // NULL = broadcast
        
        [Required]
        public string Content { get; set; } = string.Empty; // Encrypted content
        
        [Required]
        [StringLength(32)]
        public string IV { get; set; } = string.Empty; // Initialization Vector
        
        [Required]
        public MessageType MessageType { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime Timestamp { get; set; }
        
        // Navigation properties
        public virtual User Sender { get; set; } = null!;
        public virtual User? Receiver { get; set; }
    }

    public enum MessageType
    {
        Private = 1,
        Broadcast = 2
    }
}
