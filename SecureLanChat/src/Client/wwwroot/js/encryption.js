/**
 * Client-side Encryption Module
 * Sử dụng Web Crypto API cho RSA và AES encryption
 */
class ClientEncryption {
    constructor() {
        this.rsaKeyPair = null;
        this.aesKey = null;
        this.serverPublicKey = null;
        this.isInitialized = false;
    }

    /**
     * Khởi tạo encryption module
     */
    async initialize() {
        try {
            console.log('Initializing client encryption...');
            
            // Generate RSA key pair
            await this.generateRSAKeyPair();
            
            this.isInitialized = true;
            console.log('Client encryption initialized successfully');
            
            return true;
        } catch (error) {
            console.error('Failed to initialize client encryption:', error);
            throw new Error('Encryption initialization failed: ' + error.message);
        }
    }

    /**
     * Generate RSA key pair (2048-bit)
     */
    async generateRSAKeyPair() {
        try {
            console.log('Generating RSA key pair...');
            
            this.rsaKeyPair = await window.crypto.subtle.generateKey(
                {
                    name: 'RSA-OAEP',
                    modulusLength: 2048,
                    publicExponent: new Uint8Array([1, 0, 1]),
                    hash: 'SHA-256'
                },
                true,
                ['encrypt', 'decrypt']
            );

            console.log('RSA key pair generated successfully');
            return this.rsaKeyPair;
        } catch (error) {
            console.error('Failed to generate RSA key pair:', error);
            throw new Error('RSA key generation failed: ' + error.message);
        }
    }

    /**
     * Export public key as base64 string
     */
    async exportPublicKey() {
        try {
            if (!this.rsaKeyPair) {
                throw new Error('RSA key pair not generated');
            }

            const exported = await window.crypto.subtle.exportKey('spki', this.rsaKeyPair.publicKey);
            const publicKeyBase64 = this.arrayBufferToBase64(exported);
            
            console.log('Public key exported successfully');
            return publicKeyBase64;
        } catch (error) {
            console.error('Failed to export public key:', error);
            throw new Error('Public key export failed: ' + error.message);
        }
    }

    /**
     * Export private key as base64 string
     */
    async exportPrivateKey() {
        try {
            if (!this.rsaKeyPair) {
                throw new Error('RSA key pair not generated');
            }

            const exported = await window.crypto.subtle.exportKey('pkcs8', this.rsaKeyPair.privateKey);
            const privateKeyBase64 = this.arrayBufferToBase64(exported);
            
            console.log('Private key exported successfully');
            return privateKeyBase64;
        } catch (error) {
            console.error('Failed to export private key:', error);
            throw new Error('Private key export failed: ' + error.message);
        }
    }

    /**
     * Import server public key
     */
    async importServerPublicKey(serverPublicKeyBase64) {
        try {
            console.log('Importing server public key...');
            
            const keyData = this.base64ToArrayBuffer(serverPublicKeyBase64);
            
            this.serverPublicKey = await window.crypto.subtle.importKey(
                'spki',
                keyData,
                {
                    name: 'RSA-OAEP',
                    hash: 'SHA-256'
                },
                true,
                ['encrypt']
            );

            console.log('Server public key imported successfully');
            return true;
        } catch (error) {
            console.error('Failed to import server public key:', error);
            throw new Error('Server public key import failed: ' + error.message);
        }
    }

    /**
     * Generate AES key (128-bit)
     */
    async generateAESKey() {
        try {
            console.log('Generating AES key...');
            
            this.aesKey = await window.crypto.subtle.generateKey(
                {
                    name: 'AES-CBC',
                    length: 128
                },
                true,
                ['encrypt', 'decrypt']
            );

            console.log('AES key generated successfully');
            return this.aesKey;
        } catch (error) {
            console.error('Failed to generate AES key:', error);
            throw new Error('AES key generation failed: ' + error.message);
        }
    }

    /**
     * Export AES key as base64 string
     */
    async exportAESKey() {
        try {
            if (!this.aesKey) {
                throw new Error('AES key not generated');
            }

            const exported = await window.crypto.subtle.exportKey('raw', this.aesKey);
            const aesKeyBase64 = this.arrayBufferToBase64(exported);
            
            console.log('AES key exported successfully');
            return aesKeyBase64;
        } catch (error) {
            console.error('Failed to export AES key:', error);
            throw new Error('AES key export failed: ' + error.message);
        }
    }

    /**
     * Import AES key from base64 string
     */
    async importAESKey(aesKeyBase64) {
        try {
            console.log('Importing AES key...');
            
            const keyData = this.base64ToArrayBuffer(aesKeyBase64);
            
            this.aesKey = await window.crypto.subtle.importKey(
                'raw',
                keyData,
                {
                    name: 'AES-CBC',
                    length: 128
                },
                true,
                ['encrypt', 'decrypt']
            );

            console.log('AES key imported successfully');
            return true;
        } catch (error) {
            console.error('Failed to import AES key:', error);
            throw new Error('AES key import failed: ' + error.message);
        }
    }

    /**
     * Encrypt message with RSA public key
     */
    async encryptWithRSA(message, publicKeyBase64) {
        try {
            if (!message) {
                throw new Error('Message cannot be empty');
            }

            console.log('Encrypting message with RSA...');
            
            const keyData = this.base64ToArrayBuffer(publicKeyBase64);
            const publicKey = await window.crypto.subtle.importKey(
                'spki',
                keyData,
                {
                    name: 'RSA-OAEP',
                    hash: 'SHA-256'
                },
                true,
                ['encrypt']
            );

            const messageBuffer = new TextEncoder().encode(message);
            const encrypted = await window.crypto.subtle.encrypt(
                {
                    name: 'RSA-OAEP'
                },
                publicKey,
                messageBuffer
            );

            const encryptedBase64 = this.arrayBufferToBase64(encrypted);
            console.log('Message encrypted with RSA successfully');
            
            return encryptedBase64;
        } catch (error) {
            console.error('Failed to encrypt message with RSA:', error);
            throw new Error('RSA encryption failed: ' + error.message);
        }
    }

    /**
     * Decrypt message with RSA private key
     */
    async decryptWithRSA(encryptedMessageBase64) {
        try {
            if (!this.rsaKeyPair) {
                throw new Error('RSA key pair not available');
            }

            console.log('Decrypting message with RSA...');
            
            const encryptedBuffer = this.base64ToArrayBuffer(encryptedMessageBase64);
            const decrypted = await window.crypto.subtle.decrypt(
                {
                    name: 'RSA-OAEP'
                },
                this.rsaKeyPair.privateKey,
                encryptedBuffer
            );

            const decryptedMessage = new TextDecoder().decode(decrypted);
            console.log('Message decrypted with RSA successfully');
            
            return decryptedMessage;
        } catch (error) {
            console.error('Failed to decrypt message with RSA:', error);
            throw new Error('RSA decryption failed: ' + error.message);
        }
    }

    /**
     * Encrypt message with AES
     */
    async encryptWithAES(message) {
        try {
            if (!this.aesKey) {
                throw new Error('AES key not available');
            }

            if (!message) {
                throw new Error('Message cannot be empty');
            }

            console.log('Encrypting message with AES...');
            
            // Generate random IV
            const iv = window.crypto.getRandomValues(new Uint8Array(16));
            
            const messageBuffer = new TextEncoder().encode(message);
            const encrypted = await window.crypto.subtle.encrypt(
                {
                    name: 'AES-CBC',
                    iv: iv
                },
                this.aesKey,
                messageBuffer
            );

            // Combine IV and encrypted data
            const combined = new Uint8Array(iv.length + encrypted.byteLength);
            combined.set(iv);
            combined.set(new Uint8Array(encrypted), iv.length);

            const encryptedBase64 = this.arrayBufferToBase64(combined);
            console.log('Message encrypted with AES successfully');
            
            return encryptedBase64;
        } catch (error) {
            console.error('Failed to encrypt message with AES:', error);
            throw new Error('AES encryption failed: ' + error.message);
        }
    }

    /**
     * Decrypt message with AES
     */
    async decryptWithAES(encryptedMessageBase64) {
        try {
            if (!this.aesKey) {
                throw new Error('AES key not available');
            }

            console.log('Decrypting message with AES...');
            
            const combined = this.base64ToArrayBuffer(encryptedMessageBase64);
            
            // Extract IV and encrypted data
            const iv = combined.slice(0, 16);
            const encrypted = combined.slice(16);
            
            const decrypted = await window.crypto.subtle.decrypt(
                {
                    name: 'AES-CBC',
                    iv: iv
                },
                this.aesKey,
                encrypted
            );

            const decryptedMessage = new TextDecoder().decode(decrypted);
            console.log('Message decrypted with AES successfully');
            
            return decryptedMessage;
        } catch (error) {
            console.error('Failed to decrypt message with AES:', error);
            throw new Error('AES decryption failed: ' + error.message);
        }
    }

    /**
     * Encrypt AES key with RSA public key
     */
    async encryptAESKeyWithRSA(aesKeyBase64, publicKeyBase64) {
        try {
            console.log('Encrypting AES key with RSA...');
            
            const encryptedAESKey = await this.encryptWithRSA(aesKeyBase64, publicKeyBase64);
            console.log('AES key encrypted with RSA successfully');
            
            return encryptedAESKey;
        } catch (error) {
            console.error('Failed to encrypt AES key with RSA:', error);
            throw new Error('AES key encryption failed: ' + error.message);
        }
    }

    /**
     * Decrypt AES key with RSA private key
     */
    async decryptAESKeyWithRSA(encryptedAESKeyBase64) {
        try {
            console.log('Decrypting AES key with RSA...');
            
            const decryptedAESKey = await this.decryptWithRSA(encryptedAESKeyBase64);
            console.log('AES key decrypted with RSA successfully');
            
            return decryptedAESKey;
        } catch (error) {
            console.error('Failed to decrypt AES key with RSA:', error);
            throw new Error('AES key decryption failed: ' + error.message);
        }
    }

    /**
     * Validate key format
     */
    validateKey(keyBase64) {
        try {
            if (!keyBase64 || typeof keyBase64 !== 'string') {
                return false;
            }

            // Check if it's valid base64
            const decoded = this.base64ToArrayBuffer(keyBase64);
            return decoded.byteLength > 0;
        } catch (error) {
            console.error('Key validation failed:', error);
            return false;
        }
    }

    /**
     * Get encryption status
     */
    getStatus() {
        return {
            isInitialized: this.isInitialized,
            hasRSAKeyPair: !!this.rsaKeyPair,
            hasAESKey: !!this.aesKey,
            hasServerPublicKey: !!this.serverPublicKey
        };
    }

    /**
     * Reset encryption module
     */
    reset() {
        this.rsaKeyPair = null;
        this.aesKey = null;
        this.serverPublicKey = null;
        this.isInitialized = false;
        console.log('Encryption module reset');
    }

    /**
     * Utility: Convert ArrayBuffer to Base64
     */
    arrayBufferToBase64(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary);
    }

    /**
     * Utility: Convert Base64 to ArrayBuffer
     */
    base64ToArrayBuffer(base64) {
        const binary = window.atob(base64);
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
        }
        return bytes.buffer;
    }
}

// Export for use in other modules
window.ClientEncryption = ClientEncryption;
