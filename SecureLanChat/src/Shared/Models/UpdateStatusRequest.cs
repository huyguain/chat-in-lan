using System.ComponentModel.DataAnnotations;

namespace SecureLanChat.Models
{
    public class UpdateStatusRequest
    {
        [Required]
        public bool IsOnline { get; set; }
    }
}

