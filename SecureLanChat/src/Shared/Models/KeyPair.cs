namespace SecureLanChat.Models
{
    public class KeyPair
    {
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class EncryptedMessage
    {
        public string Content { get; set; } = string.Empty;
        public string IV { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string? ReceiverId { get; set; }
        public MessageType MessageType { get; set; }
    }
}
