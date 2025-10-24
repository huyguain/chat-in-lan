using SecureLanChat.Models;

namespace SecureLanChat.Interfaces
{
    public interface IEncryptionService
    {
        Task<KeyPair> GenerateKeyPairAsync();
        Task<string> EncryptMessageAsync(string message, string publicKey);
        Task<string> DecryptMessageAsync(string encryptedMessage, string privateKey);
        Task<string> ExchangePublicKeyAsync(string clientPublicKey);
        Task<string> GenerateAESKeyAsync();
        Task<string> EncryptAESKeyAsync(string aesKey, string publicKey);
        Task<string> DecryptAESKeyAsync(string encryptedAesKey, string privateKey);
        Task<bool> ValidateKeyAsync(string key);
    }
}
