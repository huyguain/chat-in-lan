/**
 * Unit tests for Client-side Encryption Module
 * Sử dụng Jest testing framework
 */

describe('ClientEncryption', () => {
    let encryption;

    beforeEach(() => {
        encryption = new ClientEncryption();
    });

    afterEach(() => {
        encryption.reset();
    });

    describe('Initialization', () => {
        test('should initialize successfully', async () => {
            const result = await encryption.initialize();
            expect(result).toBe(true);
            expect(encryption.isInitialized).toBe(true);
        });

        test('should generate RSA key pair on initialization', async () => {
            await encryption.initialize();
            expect(encryption.rsaKeyPair).not.toBeNull();
        });

        test('should throw error on initialization failure', async () => {
            // Mock crypto.subtle to throw error
            const originalSubtle = window.crypto.subtle;
            window.crypto.subtle = null;

            await expect(encryption.initialize()).rejects.toThrow('Encryption initialization failed');

            // Restore original
            window.crypto.subtle = originalSubtle;
        });
    });

    describe('RSA Key Operations', () => {
        beforeEach(async () => {
            await encryption.initialize();
        });

        test('should export public key', async () => {
            const publicKey = await encryption.exportPublicKey();
            expect(publicKey).toBeDefined();
            expect(typeof publicKey).toBe('string');
            expect(publicKey.length).toBeGreaterThan(0);
        });

        test('should export private key', async () => {
            const privateKey = await encryption.exportPrivateKey();
            expect(privateKey).toBeDefined();
            expect(typeof privateKey).toBe('string');
            expect(privateKey.length).toBeGreaterThan(0);
        });

        test('should import server public key', async () => {
            const clientPublicKey = await encryption.exportPublicKey();
            const result = await encryption.importServerPublicKey(clientPublicKey);
            expect(result).toBe(true);
            expect(encryption.serverPublicKey).not.toBeNull();
        });

        test('should throw error when exporting keys without key pair', async () => {
            encryption.rsaKeyPair = null;
            await expect(encryption.exportPublicKey()).rejects.toThrow('RSA key pair not generated');
            await expect(encryption.exportPrivateKey()).rejects.toThrow('RSA key pair not generated');
        });
    });

    describe('AES Key Operations', () => {
        test('should generate AES key', async () => {
            await encryption.generateAESKey();
            expect(encryption.aesKey).not.toBeNull();
        });

        test('should export AES key', async () => {
            await encryption.generateAESKey();
            const aesKey = await encryption.exportAESKey();
            expect(aesKey).toBeDefined();
            expect(typeof aesKey).toBe('string');
            expect(aesKey.length).toBeGreaterThan(0);
        });

        test('should import AES key', async () => {
            await encryption.generateAESKey();
            const aesKeyBase64 = await encryption.exportAESKey();
            
            encryption.aesKey = null;
            await encryption.importAESKey(aesKeyBase64);
            
            expect(encryption.aesKey).not.toBeNull();
        });

        test('should throw error when exporting AES key without key', async () => {
            await expect(encryption.exportAESKey()).rejects.toThrow('AES key not generated');
        });
    });

    describe('RSA Encryption/Decryption', () => {
        let publicKey, privateKey;

        beforeEach(async () => {
            await encryption.initialize();
            publicKey = await encryption.exportPublicKey();
            privateKey = await encryption.exportPrivateKey();
        });

        test('should encrypt and decrypt message with RSA', async () => {
            const message = 'Test message for RSA encryption';
            
            const encrypted = await encryption.encryptWithRSA(message, publicKey);
            expect(encrypted).toBeDefined();
            expect(encrypted).not.toBe(message);
            
            const decrypted = await encryption.decryptWithRSA(encrypted);
            expect(decrypted).toBe(message);
        });

        test('should throw error when encrypting empty message', async () => {
            await expect(encryption.encryptWithRSA('', publicKey)).rejects.toThrow('Message cannot be empty');
            await expect(encryption.encryptWithRSA(null, publicKey)).rejects.toThrow('Message cannot be empty');
        });

        test('should throw error when decrypting without key pair', async () => {
            encryption.rsaKeyPair = null;
            await expect(encryption.decryptWithRSA('encrypted')).rejects.toThrow('RSA key pair not available');
        });
    });

    describe('AES Encryption/Decryption', () => {
        beforeEach(async () => {
            await encryption.generateAESKey();
        });

        test('should encrypt and decrypt message with AES', async () => {
            const message = 'Test message for AES encryption';
            
            const encrypted = await encryption.encryptWithAES(message);
            expect(encrypted).toBeDefined();
            expect(encrypted).not.toBe(message);
            
            const decrypted = await encryption.decryptWithAES(encrypted);
            expect(decrypted).toBe(message);
        });

        test('should use different IVs for same message', async () => {
            const message = 'Same message';
            
            const encrypted1 = await encryption.encryptWithAES(message);
            const encrypted2 = await encryption.encryptWithAES(message);
            
            expect(encrypted1).not.toBe(encrypted2);
        });

        test('should throw error when encrypting without AES key', async () => {
            encryption.aesKey = null;
            await expect(encryption.encryptWithAES('message')).rejects.toThrow('AES key not available');
        });

        test('should throw error when decrypting without AES key', async () => {
            encryption.aesKey = null;
            await expect(encryption.decryptWithAES('encrypted')).rejects.toThrow('AES key not available');
        });
    });

    describe('Hybrid Encryption', () => {
        let publicKey, privateKey;

        beforeEach(async () => {
            await encryption.initialize();
            publicKey = await encryption.exportPublicKey();
            privateKey = await encryption.exportPrivateKey();
            await encryption.generateAESKey();
        });

        test('should encrypt AES key with RSA', async () => {
            const aesKey = await encryption.exportAESKey();
            
            const encryptedAESKey = await encryption.encryptAESKeyWithRSA(aesKey, publicKey);
            expect(encryptedAESKey).toBeDefined();
            expect(encryptedAESKey).not.toBe(aesKey);
            
            const decryptedAESKey = await encryption.decryptAESKeyWithRSA(encryptedAESKey);
            expect(decryptedAESKey).toBe(aesKey);
        });

        test('should perform complete hybrid encryption flow', async () => {
            const message = 'Test message for hybrid encryption';
            
            // 1. Encrypt message with AES
            const encryptedMessage = await encryption.encryptWithAES(message);
            
            // 2. Encrypt AES key with RSA
            const aesKey = await encryption.exportAESKey();
            const encryptedAESKey = await encryption.encryptAESKeyWithRSA(aesKey, publicKey);
            
            // 3. Decrypt AES key with RSA
            const decryptedAESKey = await encryption.decryptAESKeyWithRSA(encryptedAESKey);
            
            // 4. Import decrypted AES key
            await encryption.importAESKey(decryptedAESKey);
            
            // 5. Decrypt message with AES
            const decryptedMessage = await encryption.decryptWithAES(encryptedMessage);
            
            expect(decryptedMessage).toBe(message);
        });
    });

    describe('Key Validation', () => {
        test('should validate valid key', () => {
            const validKey = 'dGVzdA=='; // base64 for 'test'
            expect(encryption.validateKey(validKey)).toBe(true);
        });

        test('should reject invalid key', () => {
            expect(encryption.validateKey('')).toBe(false);
            expect(encryption.validateKey(null)).toBe(false);
            expect(encryption.validateKey(undefined)).toBe(false);
            expect(encryption.validateKey('invalid-base64!')).toBe(false);
        });
    });

    describe('Status and Utility', () => {
        test('should return correct status', async () => {
            const status = encryption.getStatus();
            expect(status.isInitialized).toBe(false);
            expect(status.hasRSAKeyPair).toBe(false);
            expect(status.hasAESKey).toBe(false);
            expect(status.hasServerPublicKey).toBe(false);

            await encryption.initialize();
            await encryption.generateAESKey();
            
            const updatedStatus = encryption.getStatus();
            expect(updatedStatus.isInitialized).toBe(true);
            expect(updatedStatus.hasRSAKeyPair).toBe(true);
            expect(updatedStatus.hasAESKey).toBe(true);
            expect(updatedStatus.hasServerPublicKey).toBe(false);
        });

        test('should reset properly', async () => {
            await encryption.initialize();
            await encryption.generateAESKey();
            
            encryption.reset();
            
            const status = encryption.getStatus();
            expect(status.isInitialized).toBe(false);
            expect(status.hasRSAKeyPair).toBe(false);
            expect(status.hasAESKey).toBe(false);
            expect(status.hasServerPublicKey).toBe(false);
        });
    });

    describe('Error Handling', () => {
        test('should handle encryption errors gracefully', async () => {
            await encryption.initialize();
            
            // Mock crypto.subtle.encrypt to throw error
            const originalEncrypt = window.crypto.subtle.encrypt;
            window.crypto.subtle.encrypt = jest.fn().mockRejectedValue(new Error('Encryption failed'));
            
            await expect(encryption.encryptWithRSA('message', 'invalid-key')).rejects.toThrow('RSA encryption failed');
            
            // Restore original
            window.crypto.subtle.encrypt = originalEncrypt;
        });

        test('should handle decryption errors gracefully', async () => {
            await encryption.initialize();
            
            // Mock crypto.subtle.decrypt to throw error
            const originalDecrypt = window.crypto.subtle.decrypt;
            window.crypto.subtle.decrypt = jest.fn().mockRejectedValue(new Error('Decryption failed'));
            
            await expect(encryption.decryptWithRSA('invalid-encrypted')).rejects.toThrow('RSA decryption failed');
            
            // Restore original
            window.crypto.subtle.decrypt = originalDecrypt;
        });
    });

    describe('Performance', () => {
        test('should encrypt/decrypt within reasonable time', async () => {
            await encryption.initialize();
            await encryption.generateAESKey();
            
            const message = 'Performance test message';
            const startTime = performance.now();
            
            const encrypted = await encryption.encryptWithAES(message);
            const decrypted = await encryption.decryptWithAES(encrypted);
            
            const endTime = performance.now();
            const duration = endTime - startTime;
            
            expect(decrypted).toBe(message);
            expect(duration).toBeLessThan(1000); // Should complete within 1 second
        });
    });
});
