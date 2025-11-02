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
                _logger.LogDebug("Client public key length: {Length}", clientPublicKey.Length);
                
                // Generate server key pair if not exists
                if (_serverRSA == null)
                {
                    _logger.LogInformation("Generating server RSA key pair...");
                    _serverRSA = RSA.Create(_rsaKeySize);
                    _logger.LogInformation("Server RSA key pair generated");
                }

                // Export server public key in SPKI format (compatible with Web Crypto API)
                // Also export in RSAPublicKey format for backward compatibility
                var serverPublicKeySPKI = Convert.ToBase64String(_serverRSA.ExportSubjectPublicKeyInfo());
                var serverPublicKeyRSA = Convert.ToBase64String(_serverRSA.ExportRSAPublicKey());
                
                _logger.LogDebug("Server public key exported (SPKI: {SPKILength}, RSA: {RSALength})", 
                    serverPublicKeySPKI.Length, serverPublicKeyRSA.Length);
                
                // Store client public key for later use (we'll use SPKI format since client uses Web Crypto API)
                // The client public key is in SPKI format from Web Crypto API
                try
                {
                    var clientKeyBytes = Convert.FromBase64String(clientPublicKey);
                    using var testRsa = RSA.Create();
                    // Try to import as SPKI first (Web Crypto API format)
                    try
                    {
                        testRsa.ImportSubjectPublicKeyInfo(clientKeyBytes, out _);
                        _logger.LogDebug("Client public key is in SPKI format (Web Crypto API)");
                    }
                    catch
                    {
                        // Try as RSA public key format
                        testRsa.ImportRSAPublicKey(clientKeyBytes, out _);
                        _logger.LogDebug("Client public key is in RSA format");
                    }
                }
                catch (Exception keyEx)
                {
                    _logger.LogWarning(keyEx, "Could not validate client public key format, but continuing...");
                }
                
                _loggingService.LogEncryptionEvent("key_exchange", "exchange", true, "Successfully exchanged public keys");
                
                // Return SPKI format for Web Crypto API compatibility
                return await Task.FromResult(serverPublicKeySPKI);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("key_exchange", "exchange", false, ex.Message);
                _logger.LogError(ex, "Failed to exchange public keys: {Error}", ex.Message);
                _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
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
                var publicKeyBytes = Convert.FromBase64String(publicKey);
                
                // Try to import as SPKI first (Web Crypto API format), then RSA format
                try
                {
                    rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
                    _logger.LogDebug("Imported public key as SPKI format");
                }
                catch
                {
                    try
                    {
                        rsa.ImportRSAPublicKey(publicKeyBytes, out _);
                        _logger.LogDebug("Imported public key as RSA format");
                    }
                    catch (Exception importEx)
                    {
                        _logger.LogError(importEx, "Failed to import public key in any format");
                        throw new EncryptionException("Invalid public key format", importEx);
                    }
                }
                
                var aesKeyBytes = Convert.FromBase64String(aesKey);
                var encryptedBytes = rsa.Encrypt(aesKeyBytes, RSAEncryptionPadding.OaepSHA256);
                var encryptedAesKey = Convert.ToBase64String(encryptedBytes);
                
                _loggingService.LogEncryptionEvent("aes_encryption", "encrypt", true, "Successfully encrypted AES key");
                
                return await Task.FromResult(encryptedAesKey);
            }
            catch (Exception ex)
            {
                _loggingService.LogEncryptionEvent("aes_encryption", "encrypt", false, ex.Message);
                _logger.LogError(ex, "Failed to encrypt AES key: {Error}", ex.Message);
                _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
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

                _logger.LogDebug("Validating key format. Key length: {Length}", key.Length);
                
                // Try to parse as base64
                var keyBytes = Convert.FromBase64String(key);
                _logger.LogDebug("Key parsed as base64. Bytes length: {Length}", keyBytes.Length);
                
                // Try to import as RSA public key
                using var rsa = RSA.Create();
                try
                {
                    // Try SPKI format first (Web Crypto API format)
                    rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
                    _logger.LogDebug("Key validated as SPKI format");
                    return true;
                }
                catch
                {
                    try
                    {
                        // Try RSA public key format
                        rsa.ImportRSAPublicKey(keyBytes, out _);
                        _logger.LogDebug("Key validated as RSA public key format");
                        return true;
                    }
                    catch
                    {
                        try
                        {
                            // Try as private key
                            rsa.ImportRSAPrivateKey(keyBytes, out _);
                            _logger.LogDebug("Key validated as RSA private key format");
                            return true;
                        }
                        catch (Exception keyEx)
                        {
                            _logger.LogWarning(keyEx, "Key validation failed - could not import in any format");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Key validation failed - could not parse as base64");
                return false;
            }
        }

        public void Dispose()
        {
            _serverRSA?.Dispose();
        }
    }
}
