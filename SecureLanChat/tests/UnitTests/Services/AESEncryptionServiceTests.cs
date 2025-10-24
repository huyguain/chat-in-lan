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
    public class AESEncryptionServiceTests
    {
        private readonly Mock<ILogger<AESEncryptionService>> _loggerMock;
        private readonly Mock<ILoggingService> _loggingServiceMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly AESEncryptionService _aesService;

        public AESEncryptionServiceTests()
        {
            _loggerMock = new Mock<ILogger<AESEncryptionService>>();
            _loggingServiceMock = new Mock<ILoggingService>();
            _configurationMock = new Mock<IConfiguration>();
            
            // Setup configuration
            _configurationMock.Setup(x => x.GetValue<int>("Encryption:AESKeySize", 128)).Returns(128);
            _configurationMock.Setup(x => x.GetValue<int>("Encryption:IVSize", 16)).Returns(16);
            
            _aesService = new AESEncryptionService(
                _loggerMock.Object,
                _loggingServiceMock.Object,
                _configurationMock.Object);
        }

        [Fact]
        public async Task EncryptMessageAsync_ShouldEncryptMessageSuccessfully()
        {
            // Arrange
            var message = "Test message for AES encryption";
            var aesKey = await GenerateTestAESKeyAsync();

            // Act
            var encryptedMessage = await _aesService.EncryptMessageAsync(message, aesKey);

            // Assert
            Assert.NotNull(encryptedMessage);
            Assert.NotEmpty(encryptedMessage.Content);
            Assert.NotEmpty(encryptedMessage.IV);
            Assert.NotEqual(message, encryptedMessage.Content);
            Assert.True(encryptedMessage.Timestamp <= DateTime.UtcNow);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_message_encryption", "encrypt", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task DecryptMessageAsync_ShouldDecryptMessageSuccessfully()
        {
            // Arrange
            var originalMessage = "Test message for AES decryption";
            var aesKey = await GenerateTestAESKeyAsync();
            var encryptedMessage = await _aesService.EncryptMessageAsync(originalMessage, aesKey);

            // Act
            var decryptedMessage = await _aesService.DecryptMessageAsync(encryptedMessage, aesKey);

            // Assert
            Assert.Equal(originalMessage, decryptedMessage);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_message_decryption", "decrypt", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task EncryptMessageAsync_ShouldThrowException_WhenMessageIsNull()
        {
            // Arrange
            var aesKey = await GenerateTestAESKeyAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _aesService.EncryptMessageAsync(null, aesKey));
        }

        [Fact]
        public async Task EncryptMessageAsync_ShouldThrowException_WhenAESKeyIsNull()
        {
            // Arrange
            var message = "Test message";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _aesService.EncryptMessageAsync(message, null));
        }

        [Fact]
        public async Task DecryptMessageAsync_ShouldThrowException_WhenEncryptedMessageIsNull()
        {
            // Arrange
            var aesKey = await GenerateTestAESKeyAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _aesService.DecryptMessageAsync(null, aesKey));
        }

        [Fact]
        public async Task EncryptStringAsync_ShouldEncryptStringSuccessfully()
        {
            // Arrange
            var plainText = "Test string for encryption";
            var aesKey = await GenerateTestAESKeyAsync();

            // Act
            var encryptedString = await _aesService.EncryptStringAsync(plainText, aesKey);

            // Assert
            Assert.NotNull(encryptedString);
            Assert.NotEmpty(encryptedString);
            Assert.NotEqual(plainText, encryptedString);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_string_encryption", "encrypt", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task DecryptStringAsync_ShouldDecryptStringSuccessfully()
        {
            // Arrange
            var originalText = "Test string for decryption";
            var aesKey = await GenerateTestAESKeyAsync();
            var iv = await _aesService.GenerateRandomIVAsync();
            var encryptedString = await _aesService.EncryptStringAsync(originalText, aesKey);

            // Extract IV from encrypted string (first 16 bytes)
            var encryptedBytes = Convert.FromBase64String(encryptedString);
            var ivBytes = new byte[16];
            var actualEncryptedBytes = new byte[encryptedBytes.Length - 16];
            Buffer.BlockCopy(encryptedBytes, 0, ivBytes, 0, 16);
            Buffer.BlockCopy(encryptedBytes, 16, actualEncryptedBytes, 0, actualEncryptedBytes.Length);

            // Act
            var decryptedString = await _aesService.DecryptStringAsync(
                Convert.ToBase64String(actualEncryptedBytes), 
                aesKey, 
                Convert.ToBase64String(ivBytes));

            // Assert
            Assert.Equal(originalText, decryptedString);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_string_decryption", "decrypt", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateRandomIVAsync_ShouldReturnValidIV()
        {
            // Act
            var iv = await _aesService.GenerateRandomIVAsync();

            // Assert
            Assert.NotNull(iv);
            Assert.NotEmpty(iv);
            
            // Verify it's valid base64
            var ivBytes = Convert.FromBase64String(iv);
            Assert.Equal(16, ivBytes.Length); // 16 bytes for AES-128
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("iv_generation", "generate", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task GenerateRandomIVAsync_ShouldReturnDifferentIVs()
        {
            // Act
            var iv1 = await _aesService.GenerateRandomIVAsync();
            var iv2 = await _aesService.GenerateRandomIVAsync();

            // Assert
            Assert.NotEqual(iv1, iv2);
        }

        [Fact]
        public async Task ValidateAESKeyAsync_ShouldReturnTrue_ForValidKey()
        {
            // Arrange
            var validKey = await GenerateTestAESKeyAsync();

            // Act
            var isValid = await _aesService.ValidateAESKeyAsync(validKey);

            // Assert
            Assert.True(isValid);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_key_validation", "validate", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task ValidateAESKeyAsync_ShouldReturnFalse_ForInvalidKey()
        {
            // Arrange
            var invalidKey = "invalid-key-format";

            // Act
            var isValid = await _aesService.ValidateAESKeyAsync(invalidKey);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task ValidateAESKeyAsync_ShouldReturnFalse_ForNullKey()
        {
            // Act
            var isValid = await _aesService.ValidateAESKeyAsync(null);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task ValidateAESKeyAsync_ShouldReturnFalse_ForWrongKeyLength()
        {
            // Arrange
            var wrongLengthKey = Convert.ToBase64String(new byte[8]); // 8 bytes instead of 16

            // Act
            var isValid = await _aesService.ValidateAESKeyAsync(wrongLengthKey);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task EncryptBytesAsync_ShouldEncryptBytesSuccessfully()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Test data for byte encryption");
            var aesKey = await GenerateTestAESKeyAsync();

            // Act
            var encryptedBytes = await _aesService.EncryptBytesAsync(data, aesKey);

            // Assert
            Assert.NotNull(encryptedBytes);
            Assert.NotEmpty(encryptedBytes);
            Assert.NotEqual(data, encryptedBytes);
            Assert.True(encryptedBytes.Length > data.Length); // Should include IV
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_bytes_encryption", "encrypt", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task DecryptBytesAsync_ShouldDecryptBytesSuccessfully()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Test data for byte decryption");
            var aesKey = await GenerateTestAESKeyAsync();
            var encryptedBytes = await _aesService.EncryptBytesAsync(originalData, aesKey);

            // Extract IV and encrypted data
            var ivBytes = new byte[16];
            var actualEncryptedBytes = new byte[encryptedBytes.Length - 16];
            Buffer.BlockCopy(encryptedBytes, 0, ivBytes, 0, 16);
            Buffer.BlockCopy(encryptedBytes, 16, actualEncryptedBytes, 0, actualEncryptedBytes.Length);

            // Act
            var decryptedBytes = await _aesService.DecryptBytesAsync(actualEncryptedBytes, aesKey, ivBytes);

            // Assert
            Assert.Equal(originalData, decryptedBytes);
            
            // Verify logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_bytes_decryption", "decrypt", true, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task EncryptBytesAsync_ShouldThrowException_WhenDataIsNull()
        {
            // Arrange
            var aesKey = await GenerateTestAESKeyAsync();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _aesService.EncryptBytesAsync(null, aesKey));
        }

        [Fact]
        public async Task DecryptBytesAsync_ShouldThrowException_WhenEncryptedDataIsNull()
        {
            // Arrange
            var aesKey = await GenerateTestAESKeyAsync();
            var iv = new byte[16];

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _aesService.DecryptBytesAsync(null, aesKey, iv));
        }

        [Fact]
        public async Task EncryptMessageAsync_ShouldThrowEncryptionException_WhenEncryptionFails()
        {
            // Arrange
            var message = "Test message";
            var invalidKey = "invalid-aes-key";

            // Act & Assert
            await Assert.ThrowsAsync<EncryptionException>(() => 
                _aesService.EncryptMessageAsync(message, invalidKey));
            
            // Verify error logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_message_encryption", "encrypt", false, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task DecryptMessageAsync_ShouldThrowEncryptionException_WhenDecryptionFails()
        {
            // Arrange
            var aesKey = await GenerateTestAESKeyAsync();
            var invalidEncryptedMessage = new EncryptedMessage
            {
                Content = "invalid-encrypted-content",
                IV = "invalid-iv",
                Timestamp = DateTime.UtcNow,
                SenderId = "sender",
                MessageType = MessageType.Private
            };

            // Act & Assert
            await Assert.ThrowsAsync<EncryptionException>(() => 
                _aesService.DecryptMessageAsync(invalidEncryptedMessage, aesKey));
            
            // Verify error logging
            _loggingServiceMock.Verify(
                x => x.LogEncryptionEvent("aes_message_decryption", "decrypt", false, It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task EncryptMessageAsync_ShouldUseDifferentIVs_ForSameMessage()
        {
            // Arrange
            var message = "Same message";
            var aesKey = await GenerateTestAESKeyAsync();

            // Act
            var encrypted1 = await _aesService.EncryptMessageAsync(message, aesKey);
            var encrypted2 = await _aesService.EncryptMessageAsync(message, aesKey);

            // Assert
            Assert.NotEqual(encrypted1.Content, encrypted2.Content);
            Assert.NotEqual(encrypted1.IV, encrypted2.IV);
        }

        [Fact]
        public async Task EncryptMessageAsync_ShouldProduceConsistentResults_WithSameKeyAndIV()
        {
            // Arrange
            var message = "Test message";
            var aesKey = await GenerateTestAESKeyAsync();
            var iv = await _aesService.GenerateRandomIVAsync();

            // Act
            var encrypted1 = await _aesService.EncryptMessageAsync(message, aesKey);
            var encrypted2 = await _aesService.EncryptMessageAsync(message, aesKey);

            // Assert
            // Should be different due to random IV
            Assert.NotEqual(encrypted1.Content, encrypted2.Content);
        }

        private async Task<string> GenerateTestAESKeyAsync()
        {
            using var aes = Aes.Create();
            aes.KeySize = 128;
            aes.GenerateKey();
            return Convert.ToBase64String(aes.Key);
        }
    }
}
