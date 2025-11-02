using SecureLanChat.Models;

namespace SecureLanChat.Interfaces
{
    public interface IAESEncryptionService
    {
        Task<EncryptedMessage> EncryptMessageAsync(string message, string aesKey);
        Task<string> DecryptMessageAsync(EncryptedMessage encryptedMessage, string aesKey);
        Task<string> EncryptStringAsync(string plainText, string aesKey);
        Task<string> DecryptStringAsync(string encryptedText, string aesKey, string iv);
        Task<string> GenerateRandomIVAsync();
        Task<bool> ValidateAESKeyAsync(string aesKey);
        Task<byte[]> EncryptBytesAsync(byte[] data, string aesKey);
        Task<byte[]> DecryptBytesAsync(byte[] encryptedData, string aesKey, byte[] iv);
    }
}

