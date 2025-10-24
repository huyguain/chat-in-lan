using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SecureLanChat.Exceptions;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Services;
using Xunit;

namespace SecureLanChat.Tests.Services
{
    public class EncryptionServiceTests
    {
        private readonly Mock<ILogger<EncryptionService>> _loggerMock;
        private readonly Mock<ILoggingService> _loggingServiceMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly EncryptionService _encryptionService;

        public EncryptionServiceTests()
        {
            _loggerMock = new Mock<ILogger<EncryptionService>>();
            _loggingServiceMock = new Mock<ILoggingService>();
            _configurationMock = new Mock<IConfiguration>();
            
            // Setup configuration
            _configurationMock.Setup(x => x.GetValue<int>("Encryption:RSAKeySize", 2048)).Returns(2048);
            _configurationMock.Setup(x => x.GetValue<int>("Encryption:AESKeySize", 128)).Returns(128);
            
            _encryptionService = new EncryptionService(
                _loggerMock.Object,
                _loggingServiceMock.Object,
                _configurationMock.Object);
        }

        [Fact]
        public async Task GenerateKeyPairAsync_ShouldReturnValidKeyPair()
        {
            // Act
            var result = await _encryptionService.GenerateKeyPairAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.PublicKey);
            Assert.NotEmpty(result.PrivateKey);
            Assert.True(result.CreatedAt <= DateTime.UtcNow);
            Assert.True(result.ExpiresAt > DateTime.UtcNow);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("key_generation", "key_generation", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateKeyPairAsync_ShouldGenerateDifferentKeysEachTime()
        {
            // Act
            var keyPair1 = await _encryptionService.GenerateKeyPairAsync();
            var keyPair2 = await _encryptionService.GenerateKeyPairAsync();

            // Assert
            Assert.NotEqual(keyPair1.PublicKey, keyPair2.PublicKey);
            Assert.NotEqual(keyPair1.PrivateKey, keyPair2.PrivateKey);
        }

        [Fact]
        public async Task EncryptMessageAsync_ShouldEncryptMessageSuccessfully()
        {
            // Arrange
            var keyPair = await _encryptionService.GenerateKeyPairAsync();
            var message = "Test message for encryption";

            // Act
            var encryptedMessage = await _encryptionService.EncryptMessageAsync(message, keyPair.PublicKey);

            // Assert
            Assert.NotNull(encryptedMessage);
            Assert.NotEmpty(encryptedMessage);
            Assert.NotEqual(message, encryptedMessage);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("message_encryption", "encrypt", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task EncryptMessageAsync_ShouldThrowException_WhenMessageIsNull()
        {
            // Arrange
            var keyPair = await _encryptionService.GenerateKeyPairAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _encryptionService.EncryptMessageAsync(null, keyPair.PublicKey));
        }

        [Fact]
        public async Task EncryptMessageAsync_ShouldThrowException_WhenPublicKeyIsNull()
        {
            // Arrange
            var message = "Test message";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _encryptionService.EncryptMessageAsync(message, null));
        }

        [Fact]
        public async Task DecryptMessageAsync_ShouldDecryptMessageSuccessfully()
        {
            // Arrange
            var keyPair = await _encryptionService.GenerateKeyPairAsync();
            var originalMessage = "Test message for decryption";
            var encryptedMessage = await _encryptionService.EncryptMessageAsync(originalMessage, keyPair.PublicKey);

            // Act
            var decryptedMessage = await _encryptionService.DecryptMessageAsync(encryptedMessage, keyPair.PrivateKey);

            // Assert
            Assert.Equal(originalMessage, decryptedMessage);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("message_decryption", "decrypt", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task DecryptMessageAsync_ShouldThrowException_WhenEncryptedMessageIsNull()
        {
            // Arrange
            var keyPair = await _encryptionService.GenerateKeyPairAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _encryptionService.DecryptMessageAsync(null, keyPair.PrivateKey));
        }

        [Fact]
        public async Task DecryptMessageAsync_ShouldThrowException_WhenPrivateKeyIsNull()
        {
            // Arrange
            var encryptedMessage = "encrypted message";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _encryptionService.DecryptMessageAsync(encryptedMessage, null));
        }

        [Fact]
        public async Task ExchangePublicKeyAsync_ShouldReturnServerPublicKey()
        {
            // Arrange
            var clientKeyPair = await _encryptionService.GenerateKeyPairAsync();

            // Act
            var serverPublicKey = await _encryptionService.ExchangePublicKeyAsync(clientKeyPair.PublicKey);

            // Assert
            Assert.NotNull(serverPublicKey);
            Assert.NotEmpty(serverPublicKey);
            Assert.NotEqual(clientKeyPair.PublicKey, serverPublicKey);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("key_exchange", "exchange", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task ExchangePublicKeyAsync_ShouldThrowException_WhenClientPublicKeyIsNull()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _encryptionService.ExchangePublicKeyAsync(null));
        }

        [Fact]
        public async Task ExchangePublicKeyAsync_ShouldThrowException_WhenClientPublicKeyIsInvalid()
        {
            // Arrange
            var invalidKey = "invalid-key-format";

            // Act & Assert
            await Assert.ThrowsAsync<EncryptionException>(() => 
                _encryptionService.ExchangePublicKeyAsync(invalidKey));
        }

        [Fact]
        public async Task GenerateAESKeyAsync_ShouldReturnValidAESKey()
        {
            // Act
            var aesKey = await _encryptionService.GenerateAESKeyAsync();

            // Assert
            Assert.NotNull(aesKey);
            Assert.NotEmpty(aesKey);
            
            // Verify it's valid base64
            var keyBytes = Convert.FromBase64String(aesKey);
            Assert.True(keyBytes.Length > 0);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_generation", "generate", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task EncryptAESKeyAsync_ShouldEncryptAESKeySuccessfully()
        {
            // Arrange
            var keyPair = await _encryptionService.GenerateKeyPairAsync();
            var aesKey = await _encryptionService.GenerateAESKeyAsync();

            // Act
            var encryptedAesKey = await _encryptionService.EncryptAESKeyAsync(aesKey, keyPair.PublicKey);

            // Assert
            Assert.NotNull(encryptedAesKey);
            Assert.NotEmpty(encryptedAesKey);
            Assert.NotEqual(aesKey, encryptedAesKey);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_encryption", "encrypt", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task DecryptAESKeyAsync_ShouldDecryptAESKeySuccessfully()
        {
            // Arrange
            var keyPair = await _encryptionService.GenerateKeyPairAsync();
            var originalAesKey = await _encryptionService.GenerateAESKeyAsync();
            var encryptedAesKey = await _encryptionService.EncryptAESKeyAsync(originalAesKey, keyPair.PublicKey);

            // Act
            var decryptedAesKey = await _encryptionService.DecryptAESKeyAsync(encryptedAesKey, keyPair.PrivateKey);

            // Assert
            Assert.Equal(originalAesKey, decryptedAesKey);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_decryption", "decrypt", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task ValidateKeyAsync_ShouldReturnTrue_ForValidPublicKey()
        {
            // Arrange
            var keyPair = await _encryptionService.GenerateKeyPairAsync();

            // Act
            var isValid = await _encryptionService.ValidateKeyAsync(keyPair.PublicKey);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task ValidateKeyAsync_ShouldReturnTrue_ForValidPrivateKey()
        {
            // Arrange
            var keyPair = await _encryptionService.GenerateKeyPairAsync();

            // Act
            var isValid = await _encryptionService.ValidateKeyAsync(keyPair.PrivateKey);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task ValidateKeyAsync_ShouldReturnFalse_ForInvalidKey()
        {
            // Arrange
            var invalidKey = "invalid-key-format";

            // Act
            var isValid = await _encryptionService.ValidateKeyAsync(invalidKey);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task ValidateKeyAsync_ShouldReturnFalse_ForNullKey()
        {
            // Act
            var isValid = await _encryptionService.ValidateKeyAsync(null);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task ValidateKeyAsync_ShouldReturnFalse_ForEmptyKey()
        {
            // Act
            var isValid = await _encryptionService.ValidateKeyAsync(string.Empty);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task EncryptMessageAsync_ShouldThrowEncryptionException_WhenEncryptionFails()
        {
            // Arrange
            var invalidPublicKey = "invalid-public-key";
            var message = "Test message";

            // Act & Assert
            await Assert.ThrowsAsync<EncryptionException>(() => 
                _encryptionService.EncryptMessageAsync(message, invalidPublicKey));
            
            // Verify error logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("message_encryption", "encrypt", false, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task DecryptMessageAsync_ShouldThrowEncryptionException_WhenDecryptionFails()
        {
            // Arrange
            var keyPair = await _encryptionService.GenerateKeyPairAsync();
            var invalidEncryptedMessage = "invalid-encrypted-message";

            // Act & Assert
            await Assert.ThrowsAsync<EncryptionException>(() => 
                _encryptionService.DecryptMessageAsync(invalidEncryptedMessage, keyPair.PrivateKey));
            
            // Verify error logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("message_decryption", "decrypt", false, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateKeyPairAsync_ShouldLogEncryptionEvent_OnSuccess()
        {
            // Act
            await _encryptionService.GenerateKeyPairAsync();

            // Assert
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("key_generation", "key_generation", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateKeyPairAsync_ShouldLogEncryptionEvent_OnFailure()
        {
            // Arrange - Create service with invalid configuration to force failure
            var invalidConfigMock = new Mock<IConfiguration>();
            invalidConfigMock.Setup(x => x.GetValue<int>("Encryption:RSAKeySize", 2048)).Returns(-1); // Invalid key size
            
            var encryptionService = new EncryptionService(
                _loggerMock.Object,
                _loggingServiceMock.Object,
                invalidConfigMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<EncryptionException>(() => 
                encryptionService.GenerateKeyPairAsync());
            
            // Verify error logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("key_generation", "key_generation", false, It.IsAny<string>()),
                Times.Once);
        }
    }
}
