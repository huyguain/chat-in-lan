using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Exceptions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SecureLanChat.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly ILogger<EncryptionService> _logger;
        private readonly ILoggingService _loggingService;
        private readonly IConfiguration _configuration;
        private RSA? _serverRSA;
        private readonly int _rsaKeySize;
        private readonly int _aesKeySize;

        public EncryptionService(
            ILogger<EncryptionService> logger,
            ILoggingService loggingService,
            IConfiguration configuration)
        {
            _logger = logger;
            _loggingService = loggingService;
            _configuration = configuration;
            _rsaKeySize = _configuration.GetValue<int>("Encryption:RSAKeySize", 2048);
            _aesKeySize = _configuration.GetValue<int>("Encryption:AESKeySize", 128);
        }

        public async Task<KeyPair> GenerateKeyPairAsync()
        {
            try
            {
                _logger.LogInformation("Generating RSA key pair with {KeySize} bits", _rsaKeySize);
                
                using var rsa = RSA.Create(_rsaKeySize);
                
                var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
                var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
                
                var keyPair = new KeyPair
                {
                    PublicKey = publicKey,
                    PrivateKey = privateKey,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24) // Keys expire after 24 hours
                };

                _loggingService.LogEncryptionEvent("key_generation", "key_generation", true, $"Generated {_rsaKeySize}-bit RSA key pair");
                
                _logger.LogInformation("Successfully generated RSA key pair");
                return await Task.FromResult(keyPair);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("key_generation", "key_generation", false, ex.Message);
                _logger.LogError(ex, "Failed to generate RSA key pair");
                throw new EncryptionException("Failed to generate RSA key pair", ex);
            }
        }

        public async Task<string> EncryptMessageAsync(string message, string publicKey)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    throw new ArgumentException("Message cannot be null or empty", nameof(message));
                
                if (string.IsNullOrEmpty(publicKey))
                    throw new ArgumentException("Public key cannot be null or empty", nameof(publicKey));

                _logger.LogDebug("Encrypting message with RSA public key");
                
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);
                
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var encryptedBytes = rsa.Encrypt(messageBytes, RSAEncryptionPadding.OaepSHA256);
                var encryptedMessage = Convert.ToBase64String(encryptedBytes);
                
                _loggingService.LogEncryptionEvent("message_encryption", "encrypt", true, $"Encrypted message of {message.Length} characters");
                
                return await Task.FromResult(encryptedMessage);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("message_encryption", "encrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to encrypt message");
                throw new EncryptionException("Failed to encrypt message", ex);
            }
        }

        public async Task<string> DecryptMessageAsync(string encryptedMessage, string privateKey)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedMessage))
                    throw new ArgumentException("Encrypted message cannot be null or empty", nameof(encryptedMessage));
                
                if (string.IsNullOrEmpty(privateKey))
                    throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));

                _logger.LogDebug("Decrypting message with RSA private key");
                
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);
                
                var encryptedBytes = Convert.FromBase64String(encryptedMessage);
                var decryptedBytes = rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.OaepSHA256);
                var decryptedMessage = Encoding.UTF8.GetString(decryptedBytes);
                
                _loggingService.LogEncryptionEvent("message_decryption", "decrypt", true, $"Decrypted message of {decryptedMessage.Length} characters");
                
                return await Task.FromResult(decryptedMessage);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("message_decryption", "decrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to decrypt message");
                throw new EncryptionException("Failed to decrypt message", ex);
            }
        }

        public async Task<string> ExchangePublicKeyAsync(string clientPublicKey)
        {
            try
            {
                if (string.IsNullOrEmpty(clientPublicKey))
                    throw new ArgumentException("Client public key cannot be null or empty", nameof(clientPublicKey));

                _logger.LogInformation("Exchanging public keys with client");
                
                // Validate client public key
                if (!await ValidateKeyAsync(clientPublicKey))
                {
                    throw new EncryptionException("Invalid client public key format");
                }

                // Generate server key pair if not exists
                if (_serverRSA == null)
                {
                    _serverRSA = RSA.Create(_rsaKeySize);
                }

                var serverPublicKey = Convert.ToBase64String(_serverRSA.ExportRSAPublicKey());
                
                _loggingService.LogEncryptionEvent("key_exchange", "exchange", true, "Successfully exchanged public keys");
                
                return await Task.FromResult(serverPublicKey);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("key_exchange", "exchange", false, ex.Message);
                _logger.LogError(ex, "Failed to exchange public keys");
                throw new EncryptionException("Failed to exchange public keys", ex);
            }
        }

        public async Task<string> GenerateAESKeyAsync()
        {
            try
            {
                _logger.LogDebug("Generating AES key with {KeySize} bits", _aesKeySize);
                
                using var aes = Aes.Create();
                aes.KeySize = _aesKeySize;
                aes.GenerateKey();
                
                var aesKey = Convert.ToBase64String(aes.Key);
                
                _loggingService.LogEncryptionEvent("aes_generation", "generate", true, $"Generated {_aesKeySize}-bit AES key");
                
                return await Task.FromResult(aesKey);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("aes_generation", "generate", false, ex.Message);
                _logger.LogError(ex, "Failed to generate AES key");
                throw new EncryptionException("Failed to generate AES key", ex);
            }
        }

        public async Task<string> EncryptAESKeyAsync(string aesKey, string publicKey)
        {
            try
            {
                if (string.IsNullOrEmpty(aesKey))
                    throw new ArgumentException("AES key cannot be null or empty", nameof(aesKey));
                
                if (string.IsNullOrEmpty(publicKey))
                    throw new ArgumentException("Public key cannot be null or empty", nameof(publicKey));

                _logger.LogDebug("Encrypting AES key with RSA public key");
                
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);
                
                var aesKeyBytes = Convert.FromBase64String(aesKey);
                var encryptedBytes = rsa.Encrypt(aesKeyBytes, RSAEncryptionPadding.OaepSHA256);
                var encryptedAesKey = Convert.ToBase64String(encryptedBytes);
                
                _loggingService.LogEncryptionEvent("aes_encryption", "encrypt", true, "Successfully encrypted AES key");
                
                return await Task.FromResult(encryptedAesKey);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("aes_encryption", "encrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to encrypt AES key");
                throw new EncryptionException("Failed to encrypt AES key", ex);
            }
        }

        public async Task<string> DecryptAESKeyAsync(string encryptedAesKey, string privateKey)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedAesKey))
                    throw new ArgumentException("Encrypted AES key cannot be null or empty", nameof(encryptedAesKey));
                
                if (string.IsNullOrEmpty(privateKey))
                    throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));

                _logger.LogDebug("Decrypting AES key with RSA private key");
                
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);
                
                var encryptedBytes = Convert.FromBase64String(encryptedAesKey);
                var decryptedBytes = rsa.Decrypt(encryptedBytes, RSAEncryptionPadding.OaepSHA256);
                var decryptedAesKey = Convert.ToBase64String(decryptedBytes);
                
                _loggingService.LogEncryptionEvent("aes_decryption", "decrypt", true, "Successfully decrypted AES key");
                
                return await Task.FromResult(decryptedAesKey);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("aes_decryption", "decrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to decrypt AES key");
                throw new EncryptionException("Failed to decrypt AES key", ex);
            }
        }

        public async Task<bool> ValidateKeyAsync(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                    return false;

                _logger.LogDebug("Validating key format");
                
                // Try to parse as base64
                var keyBytes = Convert.FromBase64String(key);
                
                // Try to import as RSA public key
                using var rsa = RSA.Create();
                try
                {
                    rsa.ImportRSAPublicKey(keyBytes, out _);
                    return true;
                }
                catch
                {
                    // Try as private key
                    try
                    {
                        rsa.ImportRSAPrivateKey(keyBytes, out _);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Key validation failed");
                return false;
            }
        }

        public void Dispose()
        {
            _serverRSA?.Dispose();
        }
    }
}
