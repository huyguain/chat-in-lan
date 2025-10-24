using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Exceptions;
using System.Security.Cryptography;
using System.Text;

namespace SecureLanChat.Services
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

    public class AESEncryptionService : IAESEncryptionService
    {
        private readonly ILogger<AESEncryptionService> _logger;
        private readonly ILoggingService _loggingService;
        private readonly IConfiguration _configuration;
        private readonly int _keySize;
        private readonly int _ivSize;

        public AESEncryptionService(
            ILogger<AESEncryptionService> logger,
            ILoggingService loggingService,
            IConfiguration configuration)
        {
            _logger = logger;
            _loggingService = loggingService;
            _configuration = configuration;
            _keySize = _configuration.GetValue<int>("Encryption:AESKeySize", 128);
            _ivSize = _configuration.GetValue<int>("Encryption:IVSize", 16);
        }

        public async Task<EncryptedMessage> EncryptMessageAsync(string message, string aesKey)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    throw new ArgumentException("Message cannot be null or empty", nameof(message));
                
                if (string.IsNullOrEmpty(aesKey))
                    throw new ArgumentException("AES key cannot be null or empty", nameof(aesKey));

                _logger.LogDebug("Encrypting message with AES-{KeySize}", _keySize);

                var keyBytes = Convert.FromBase64String(aesKey);
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var iv = await GenerateRandomIVAsync();
                var ivBytes = Convert.FromBase64String(iv);

                using var aes = Aes.Create();
                aes.KeySize = _keySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = keyBytes;
                aes.IV = ivBytes;

                using var encryptor = aes.CreateEncryptor();
                var encryptedBytes = encryptor.TransformFinalBlock(messageBytes, 0, messageBytes.Length);

                var encryptedMessage = new EncryptedMessage
                {
                    Content = Convert.ToBase64String(encryptedBytes),
                    IV = iv,
                    Timestamp = DateTime.UtcNow,
                    SenderId = string.Empty, // Will be set by caller
                    ReceiverId = null,
                    MessageType = MessageType.Private
                };

                _loggingService.LogEncryptionEvent("aes_message_encryption", "encrypt", true, 
                    $"Encrypted message of {message.Length} characters with AES-{_keySize}");

                return await Task.FromResult(encryptedMessage);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("aes_message_encryption", "encrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to encrypt message with AES");
                throw new EncryptionException("Failed to encrypt message with AES", ex);
            }
        }

        public async Task<string> DecryptMessageAsync(EncryptedMessage encryptedMessage, string aesKey)
        {
            try
            {
                if (encryptedMessage == null)
                    throw new ArgumentException("Encrypted message cannot be null", nameof(encryptedMessage));
                
                if (string.IsNullOrEmpty(aesKey))
                    throw new ArgumentException("AES key cannot be null or empty", nameof(aesKey));

                _logger.LogDebug("Decrypting message with AES-{KeySize}", _keySize);

                var keyBytes = Convert.FromBase64String(aesKey);
                var encryptedBytes = Convert.FromBase64String(encryptedMessage.Content);
                var ivBytes = Convert.FromBase64String(encryptedMessage.IV);

                using var aes = Aes.Create();
                aes.KeySize = _keySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = keyBytes;
                aes.IV = ivBytes;

                using var decryptor = aes.CreateDecryptor();
                var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                var decryptedMessage = Encoding.UTF8.GetString(decryptedBytes);

                _loggingService.LogEncryptionEvent("aes_message_decryption", "decrypt", true, 
                    $"Decrypted message of {decryptedMessage.Length} characters with AES-{_keySize}");

                return await Task.FromResult(decryptedMessage);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("aes_message_decryption", "decrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to decrypt message with AES");
                throw new EncryptionException("Failed to decrypt message with AES", ex);
            }
        }

        public async Task<string> EncryptStringAsync(string plainText, string aesKey)
        {
            try
            {
                if (string.IsNullOrEmpty(plainText))
                    throw new ArgumentException("Plain text cannot be null or empty", nameof(plainText));
                
                if (string.IsNullOrEmpty(aesKey))
                    throw new ArgumentException("AES key cannot be null or empty", nameof(aesKey));

                _logger.LogDebug("Encrypting string with AES-{KeySize}", _keySize);

                var keyBytes = Convert.FromBase64String(aesKey);
                var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                var iv = await GenerateRandomIVAsync();
                var ivBytes = Convert.FromBase64String(iv);

                using var aes = Aes.Create();
                aes.KeySize = _keySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = keyBytes;
                aes.IV = ivBytes;

                using var encryptor = aes.CreateEncryptor();
                var encryptedBytes = encryptor.TransformFinalBlock(plainTextBytes, 0, plainTextBytes.Length);

                // Combine IV and encrypted data
                var result = new byte[ivBytes.Length + encryptedBytes.Length];
                Buffer.BlockCopy(ivBytes, 0, result, 0, ivBytes.Length);
                Buffer.BlockCopy(encryptedBytes, 0, result, ivBytes.Length, encryptedBytes.Length);

                var encryptedString = Convert.ToBase64String(result);

                _loggingService.LogEncryptionEvent("aes_string_encryption", "encrypt", true, 
                    $"Encrypted string of {plainText.Length} characters");

                return await Task.FromResult(encryptedString);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("aes_string_encryption", "encrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to encrypt string with AES");
                throw new EncryptionException("Failed to encrypt string with AES", ex);
            }
        }

        public async Task<string> DecryptStringAsync(string encryptedText, string aesKey, string iv)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedText))
                    throw new ArgumentException("Encrypted text cannot be null or empty", nameof(encryptedText));
                
                if (string.IsNullOrEmpty(aesKey))
                    throw new ArgumentException("AES key cannot be null or empty", nameof(aesKey));
                
                if (string.IsNullOrEmpty(iv))
                    throw new ArgumentException("IV cannot be null or empty", nameof(iv));

                _logger.LogDebug("Decrypting string with AES-{KeySize}", _keySize);

                var keyBytes = Convert.FromBase64String(aesKey);
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var ivBytes = Convert.FromBase64String(iv);

                using var aes = Aes.Create();
                aes.KeySize = _keySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = keyBytes;
                aes.IV = ivBytes;

                using var decryptor = aes.CreateDecryptor();
                var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                var decryptedString = Encoding.UTF8.GetString(decryptedBytes);

                _loggingService.LogEncryptionEvent("aes_string_decryption", "decrypt", true, 
                    $"Decrypted string of {decryptedString.Length} characters");

                return await Task.FromResult(decryptedString);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("aes_string_decryption", "decrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to decrypt string with AES");
                throw new EncryptionException("Failed to decrypt string with AES", ex);
            }
        }

        public async Task<string> GenerateRandomIVAsync()
        {
            try
            {
                _logger.LogDebug("Generating random IV with {IVSize} bytes", _ivSize);

                var iv = new byte[_ivSize];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(iv);

                var ivString = Convert.ToBase64String(iv);

                _loggingService.LogEncryptionEvent("iv_generation", "generate", true, 
                    $"Generated {_ivSize}-byte IV");

                return await Task.FromResult(ivString);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("iv_generation", "generate", false, ex.Message);
                _logger.LogError(ex, "Failed to generate random IV");
                throw new EncryptionException("Failed to generate random IV", ex);
            }
        }

        public async Task<bool> ValidateAESKeyAsync(string aesKey)
        {
            try
            {
                if (string.IsNullOrEmpty(aesKey))
                    return false;

                _logger.LogDebug("Validating AES key format");

                var keyBytes = Convert.FromBase64String(aesKey);
                
                // Check key length matches expected size
                var expectedKeyLength = _keySize / 8; // Convert bits to bytes
                if (keyBytes.Length != expectedKeyLength)
                {
                    _logger.LogDebug("AES key length mismatch. Expected: {Expected}, Actual: {Actual}", 
                        expectedKeyLength, keyBytes.Length);
                    return false;
                }

                _loggingService.LogEncryptionEvent("aes_key_validation", "validate", true, 
                    $"Validated AES-{_keySize} key");

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "AES key validation failed");
                return false;
            }
        }

        public async Task<byte[]> EncryptBytesAsync(byte[] data, string aesKey)
        {
            try
            {
                if (data == null || data.Length == 0)
                    throw new ArgumentException("Data cannot be null or empty", nameof(data));
                
                if (string.IsNullOrEmpty(aesKey))
                    throw new ArgumentException("AES key cannot be null or empty", nameof(aesKey));

                _logger.LogDebug("Encrypting {DataLength} bytes with AES-{KeySize}", data.Length, _keySize);

                var keyBytes = Convert.FromBase64String(aesKey);
                var iv = await GenerateRandomIVAsync();
                var ivBytes = Convert.FromBase64String(iv);

                using var aes = Aes.Create();
                aes.KeySize = _keySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = keyBytes;
                aes.IV = ivBytes;

                using var encryptor = aes.CreateEncryptor();
                var encryptedBytes = encryptor.TransformFinalBlock(data, 0, data.Length);

                // Combine IV and encrypted data
                var result = new byte[ivBytes.Length + encryptedBytes.Length];
                Buffer.BlockCopy(ivBytes, 0, result, 0, ivBytes.Length);
                Buffer.BlockCopy(encryptedBytes, 0, result, ivBytes.Length, encryptedBytes.Length);

                _loggingService.LogEncryptionEvent("aes_bytes_encryption", "encrypt", true, 
                    $"Encrypted {data.Length} bytes");

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("aes_bytes_encryption", "encrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to encrypt bytes with AES");
                throw new EncryptionException("Failed to encrypt bytes with AES", ex);
            }
        }

        public async Task<byte[]> DecryptBytesAsync(byte[] encryptedData, string aesKey, byte[] iv)
        {
            try
            {
                if (encryptedData == null || encryptedData.Length == 0)
                    throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));
                
                if (string.IsNullOrEmpty(aesKey))
                    throw new ArgumentException("AES key cannot be null or empty", nameof(aesKey));
                
                if (iv == null || iv.Length == 0)
                    throw new ArgumentException("IV cannot be null or empty", nameof(iv));

                _logger.LogDebug("Decrypting {DataLength} bytes with AES-{KeySize}", encryptedData.Length, _keySize);

                var keyBytes = Convert.FromBase64String(aesKey);

                using var aes = Aes.Create();
                aes.KeySize = _keySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = keyBytes;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                var decryptedBytes = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);

                _loggingService.LogEncryptionEvent("aes_bytes_decryption", "decrypt", true, 
                    $"Decrypted {decryptedBytes.Length} bytes");

                return await Task.FromResult(decryptedBytes);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("aes_bytes_decryption", "decrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to decrypt bytes with AES");
                throw new EncryptionException("Failed to decrypt bytes with AES", ex);
            }
        }
    }
}
