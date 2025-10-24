# Encryption System Documentation

## Tổng quan

Hệ thống mã hóa sử dụng hybrid encryption với RSA cho key exchange và AES cho message encryption. Đảm bảo bảo mật end-to-end cho tất cả tin nhắn trong hệ thống chat.

## Kiến trúc mã hóa

### 1. Hybrid Encryption Model

```
Client                    Server
  |                         |
  |-- RSA Key Exchange ---->|
  |<-- Server Public Key ---|
  |                         |
  |-- Client Public Key --->|
  |                         |
  |<-- Encrypted AES Key ---|
  |                         |
  |-- Encrypted Message --->|
  |<-- Encrypted Message ---|
```

### 2. Key Management

- **RSA Keys**: 2048-bit cho key exchange
- **AES Keys**: 128-bit cho message encryption
- **Key Expiration**: 24 giờ cho RSA keys
- **Session Keys**: Unique AES key cho mỗi session

## RSA Key Generation và Exchange

### 1. Key Generation Process

```csharp
// Generate RSA key pair
var keyPair = await encryptionService.GenerateKeyPairAsync();

// Key pair contains:
// - PublicKey: For encryption
// - PrivateKey: For decryption
// - CreatedAt: Generation timestamp
// - ExpiresAt: Expiration timestamp (24 hours)
```

### 2. Key Exchange Protocol

1. **Client generates RSA key pair**
2. **Client sends public key to server**
3. **Server validates client public key**
4. **Server generates RSA key pair**
5. **Server sends public key to client**
6. **Both parties can now encrypt/decrypt**

### 3. Key Validation

```csharp
// Validate key format
var isValid = await encryptionService.ValidateKeyAsync(publicKey);

// Checks:
// - Base64 format
// - Valid RSA key structure
// - Proper key length
```

## AES Message Encryption

### 1. AES Key Generation

```csharp
// Generate AES key for session
var aesKey = await encryptionService.GenerateAESKeyAsync();

// AES key is 128-bit (16 bytes)
// Base64 encoded for transmission
```

### 2. Message Encryption Flow

```csharp
// 1. Generate AES key
var aesKey = await encryptionService.GenerateAESKeyAsync();

// 2. Encrypt AES key with RSA public key
var encryptedAesKey = await encryptionService.EncryptAESKeyAsync(aesKey, publicKey);

// 3. Encrypt message with AES
var encryptedMessage = await aesService.EncryptMessageAsync(message, aesKey);

// 4. Send encrypted message + encrypted AES key
```

### 3. Message Decryption Flow

```csharp
// 1. Decrypt AES key with RSA private key
var aesKey = await encryptionService.DecryptAESKeyAsync(encryptedAesKey, privateKey);

// 2. Decrypt message with AES
var message = await aesService.DecryptMessageAsync(encryptedMessage, aesKey);
```

## Key Storage và Management

### 1. Key Storage Service

```csharp
// Store user public key
await keyStorageService.StoreUserPublicKeyAsync(userId, publicKey);

// Store session AES key
await keyStorageService.StoreSessionKeyAsync(userId, connectionId, aesKey);

// Retrieve keys
var publicKey = await keyStorageService.GetUserPublicKeyAsync(userId);
var sessionKey = await keyStorageService.GetSessionKeyAsync(userId, connectionId);
```

### 2. Key Lifecycle Management

- **RSA Keys**: Generated per session, expire after 24 hours
- **AES Keys**: Generated per connection, expire after 24 hours
- **Automatic Cleanup**: Expired keys are automatically removed
- **Key Rotation**: New keys generated for each session

### 3. Key Validation

```csharp
// Check if key exists
var keyExists = await keyStorageService.KeyExistsAsync(userId, "public");

// Validate stored key
var isValid = await keyStorageService.ValidateStoredKeyAsync(userId, "session");
```

## Security Features

### 1. Forward Secrecy

- Mỗi session có AES key riêng biệt
- Keys không được tái sử dụng
- Session keys expire sau 24 giờ

### 2. Key Exchange Security

- RSA 2048-bit cho key exchange
- OAEP padding cho RSA encryption
- Key validation trước khi sử dụng

### 3. Message Security

- AES-128 cho message encryption
- Random IV cho mỗi message
- Base64 encoding cho transmission

## Error Handling

### 1. Encryption Exceptions

```csharp
public class EncryptionException : ChatException
{
    // Encryption/decryption failures
    // Invalid key formats
    // Key generation errors
}
```

### 2. Common Error Scenarios

- **Invalid Key Format**: Key không đúng format
- **Key Generation Failure**: Lỗi tạo key
- **Encryption Failure**: Lỗi mã hóa message
- **Decryption Failure**: Lỗi giải mã message
- **Key Exchange Failure**: Lỗi trao đổi key

### 3. Error Recovery

```csharp
try
{
    var encryptedMessage = await encryptionService.EncryptMessageAsync(message, publicKey);
}
catch (EncryptionException ex)
{
    // Log error
    // Generate new key pair
    // Retry encryption
}
```

## Performance Considerations

### 1. Key Generation Performance

- **RSA Key Generation**: ~100-200ms cho 2048-bit
- **AES Key Generation**: ~1-2ms
- **Key Validation**: ~1-5ms

### 2. Encryption Performance

- **RSA Encryption**: ~10-50ms cho small messages
- **AES Encryption**: ~1-5ms cho messages
- **Hybrid Approach**: Best of both worlds

### 3. Memory Usage

- **RSA Keys**: ~2KB per key pair
- **AES Keys**: ~16 bytes per key
- **Key Storage**: Minimal database impact

## Configuration

### 1. Encryption Settings

```json
{
  "Encryption": {
    "AESKeySize": 128,
    "RSAKeySize": 2048,
    "IVSize": 16
  }
}
```

### 2. Key Expiration

```csharp
// RSA keys expire after 24 hours
var keyPair = new KeyPair
{
    CreatedAt = DateTime.UtcNow,
    ExpiresAt = DateTime.UtcNow.AddHours(24)
};

// Session keys expire after 24 hours
var session = new Session
{
    CreatedAt = DateTime.UtcNow,
    ExpiresAt = DateTime.UtcNow.AddHours(24)
};
```

## Testing

### 1. Unit Tests

- **Key Generation Tests**: Verify key generation
- **Encryption Tests**: Test encrypt/decrypt cycle
- **Key Validation Tests**: Test key format validation
- **Error Handling Tests**: Test exception scenarios

### 2. Integration Tests

- **End-to-End Encryption**: Complete encryption flow
- **Key Exchange Tests**: Client-server key exchange
- **Performance Tests**: Encryption performance benchmarks

### 3. Security Tests

- **Key Strength Tests**: Verify key strength
- **Timing Attack Tests**: Test for timing vulnerabilities
- **Key Leakage Tests**: Ensure keys are not leaked

## Best Practices

### 1. Key Management

- **Never log private keys**
- **Use secure random generation**
- **Implement key rotation**
- **Monitor key usage**

### 2. Encryption Practices

- **Always use fresh IVs**
- **Validate keys before use**
- **Handle encryption errors gracefully**
- **Log security events**

### 3. Performance Optimization

- **Cache public keys**
- **Use connection pooling**
- **Implement key pre-generation**
- **Monitor encryption performance**

## Troubleshooting

### 1. Common Issues

#### Key Generation Failures
```bash
# Check system entropy
cat /proc/sys/kernel/random/entropy_avail

# Restart application if needed
systemctl restart securelanchat
```

#### Encryption Failures
```bash
# Check logs for encryption errors
grep "Encryption" logs/log-*.txt

# Verify key format
openssl rsa -in key.pem -text -noout
```

#### Performance Issues
```bash
# Monitor encryption performance
grep "Performance Metric" logs/log-*.txt

# Check memory usage
ps aux | grep SecureLanChat
```

### 2. Debug Commands

```bash
# Test key generation
curl -X POST http://localhost:5000/api/encryption/generate-key

# Test encryption
curl -X POST http://localhost:5000/api/encryption/encrypt \
  -H "Content-Type: application/json" \
  -d '{"message": "test", "publicKey": "..."}'

# Check encryption health
curl http://localhost:5000/api/health/encryption
```

## Monitoring

### 1. Key Metrics

- **Key Generation Rate**: Keys generated per minute
- **Encryption Success Rate**: Successful encryptions percentage
- **Key Expiration Rate**: Keys expiring per hour
- **Performance Metrics**: Encryption/decryption times

### 2. Alerts

- **High Encryption Failure Rate**: >5% failures
- **Slow Key Generation**: >500ms per key
- **Key Storage Issues**: Database errors
- **Security Events**: Unusual encryption patterns

### 3. Logging

```csharp
// Log encryption events
_loggingService.LogEncryptionEvent(userId, "encrypt", true, "Message encrypted successfully");

// Log key generation
_loggingService.LogEncryptionEvent(userId, "key_generation", true, "RSA key pair generated");

// Log security events
_loggingService.LogSecurityEvent("key_exchange", userId, "Public key exchanged", "INFO");
```
