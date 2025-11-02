/**
 * Chat Manager
 * Handles SignalR connection and real-time messaging
 */
class ChatManager {
    constructor(encryption) {
        this.encryption = encryption;
        this.connection = null;
        this.currentUser = null;
        this.isConnected = false;
        this.messageQueue = [];
    }

    async connect(userId) {
        try {
            console.log('Connecting to SignalR hub...');
            
            // Create SignalR connection
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/chathub", {
                    // Allow multiple transport types for better compatibility
                    // WebSockets will be tried first, then LongPolling as fallback
                })
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: retryContext => {
                        if (retryContext.elapsedMilliseconds < 60000) {
                            // If we've been reconnecting for less than 60 seconds, wait 0, 2, 10 and 30 seconds
                            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
                        }
                        // Otherwise, wait 30 seconds
                        return 30000;
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Setup event handlers
            this.setupEventHandlers();

            // Start connection with timeout
            const connectionPromise = this.connection.start();
            const timeoutPromise = new Promise((_, reject) => 
                setTimeout(() => reject(new Error('Connection timeout after 10 seconds')), 10000)
            );
            
            await Promise.race([connectionPromise, timeoutPromise]);
            
            this.isConnected = true;
            this.currentUser = { id: userId };
            
            console.log('SignalR connected, joining chat...');
            
            // Join chat
            await this.connection.invoke("JoinChat", userId);
            
            // Exchange encryption keys
            await this.exchangeKeys();
            
            console.log('Connected to SignalR hub successfully');
        } catch (error) {
            console.error('Failed to connect to SignalR hub:', error);
            console.error('Error details:', error.message);
            if (error.error) {
                console.error('SignalR error:', error.error);
            }
            throw error;
        }
    }

    setupEventHandlers() {
        // Connection events
        this.connection.onclose((error) => {
            console.log('SignalR connection closed:', error);
            this.isConnected = false;
            if (window.app) {
                window.app.updateConnectionStatus('offline');
            }
        });

        this.connection.onreconnecting((error) => {
            console.log('SignalR reconnecting:', error);
            if (window.app) {
                window.app.updateConnectionStatus('connecting');
            }
        });

        this.connection.onreconnected((connectionId) => {
            console.log('SignalR reconnected:', connectionId);
            this.isConnected = true;
            if (window.app) {
                window.app.updateConnectionStatus('online');
            }
        });

        // Chat events
        this.connection.on("ReceiveMessage", (message) => {
            this.handleMessageReceived(message);
        });

        this.connection.on("UserOnline", (user) => {
            this.handleUserOnline(user);
        });

        this.connection.on("UserOffline", (user) => {
            this.handleUserOffline(user);
        });

        this.connection.on("OnlineUsers", (users) => {
            this.handleOnlineUsers(users);
        });

        this.connection.on("MessageHistory", (messages) => {
            this.handleMessageHistory(messages);
        });

        this.connection.on("KeysExchanged", (keys) => {
            this.handleKeysExchanged(keys);
        });

        this.connection.on("Error", (error) => {
            console.error('SignalR Error received:', error);
            console.error('Error type:', typeof error);
            console.error('Error details:', error);
            this.handleError(error);
        });

        this.connection.on("Success", (message) => {
            this.handleSuccess(message);
        });
    }

    async exchangeKeys() {
        try {
            console.log('Exchanging encryption keys...');
            
            // Get client public key
            console.log('Exporting client public key...');
            const clientPublicKey = await this.encryption.exportPublicKey();
            console.log('Client public key exported. Length:', clientPublicKey.length);
            console.log('User ID:', this.currentUser.id);
            
            // Exchange keys with server
            console.log('Invoking ExchangeKeys on server...');
            await this.connection.invoke("ExchangeKeys", this.currentUser.id, clientPublicKey);
            console.log('ExchangeKeys invoked, waiting for response...');
            
        } catch (error) {
            console.error('Key exchange failed:', error);
            console.error('Error details:', {
                message: error.message,
                stack: error.stack,
                userId: this.currentUser?.id,
                hasPublicKey: !!clientPublicKey
            });
            throw error;
        }
    }

    async handleKeysExchanged(keys) {
        try {
            console.log('Keys exchanged, importing server public key...');
            console.log('Received keys object:', {
                hasServerPublicKey: !!keys.serverPublicKey,
                serverPublicKeyLength: keys.serverPublicKey?.length,
                hasEncryptedAESKey: !!keys.encryptedAESKey,
                encryptedAESKeyLength: keys.encryptedAESKey?.length,
                encryptedAESKeyPreview: keys.encryptedAESKey?.substring(0, 50)
            });
            
            // Import server public key
            console.log('Importing server public key...');
            await this.encryption.importServerPublicKey(keys.serverPublicKey);
            console.log('Server public key imported');
            
            // Decrypt AES key with RSA
            console.log('Decrypting AES key with RSA...');
            console.log('Encrypted AES key (first 100 chars):', keys.encryptedAESKey?.substring(0, 100));
            const decryptedAESKey = await this.encryption.decryptAESKeyWithRSA(keys.encryptedAESKey);
            console.log('AES key decrypted. Decrypted key length:', decryptedAESKey?.length);
            console.log('Decrypted AES key (first 50 chars):', decryptedAESKey?.substring(0, 50));
            console.log('Decrypted AES key (last 50 chars):', decryptedAESKey?.substring(Math.max(0, decryptedAESKey.length - 50)));
            
            // Validate base64 format before importing
            const base64Regex = /^[A-Za-z0-9+/]*={0,2}$/;
            const cleanedKey = decryptedAESKey.trim().replace(/\s+/g, '');
            if (!base64Regex.test(cleanedKey)) {
                console.error('Invalid base64 format in decrypted AES key!');
                console.error('Key preview:', cleanedKey.substring(0, 100));
                throw new Error('Decrypted AES key is not valid base64 format');
            }
            
            // Import AES key
            console.log('Importing decrypted AES key...');
            await this.encryption.importAESKey(cleanedKey);
            
            console.log('Encryption setup completed');
            
            // Process any queued messages
            this.processMessageQueue();
            
        } catch (error) {
            console.error('Failed to handle key exchange:', error);
            console.error('Error stack:', error.stack);
            console.error('Keys received:', {
                serverPublicKey: keys.serverPublicKey?.substring(0, 50),
                encryptedAESKey: keys.encryptedAESKey?.substring(0, 50)
            });
            if (window.app) {
                window.app.showNotification('Encryption setup failed: ' + error.message, 'error');
            }
        }
    }

    async sendMessage(content, receiverId = null) {
        try {
            if (!this.isConnected) {
                throw new Error('Not connected to chat');
            }

            if (!this.encryption.aesKey) {
                // Queue message if encryption not ready
                this.messageQueue.push({ content, receiverId });
                return;
            }

            console.log('Sending encrypted message...');
            console.log('Message content:', content);
            console.log('Receiver ID:', receiverId || 'broadcast');
            
            // Encrypt message with AES
            const encryptedMessage = await this.encryption.encryptWithAES(content);
            console.log('Message encrypted. Encrypted length:', encryptedMessage.length);
            console.log('Encrypted preview:', encryptedMessage.substring(0, 50));
            
            // Send to server
            console.log('Invoking SendMessage on server...');
            try {
                await this.connection.invoke("SendMessage", 
                    this.currentUser.id, 
                    receiverId, 
                    encryptedMessage, 
                    "text"
                );
                console.log('SendMessage invoked successfully');
            } catch (invokeError) {
                console.error('SendMessage invoke failed:', invokeError);
                throw invokeError;
            }
            
            // Add to UI immediately
            this.addMessageToUI({
                id: Date.now().toString(),
                senderId: this.currentUser.id,
                content: content,
                messageType: 'text',
                timestamp: new Date().toISOString(),
                isOwn: true
            });
            
        } catch (error) {
            console.error('Failed to send message:', error);
            console.error('Error details:', {
                message: error.message,
                stack: error.stack,
                name: error.name
            });
            if (window.app) {
                window.app.showNotification('Failed to send message: ' + (error.message || 'Unknown error'), 'error');
            }
        }
    }

    async handleMessageReceived(message) {
        try {
            console.log('Message received:', message);
            console.log('Message content (first 100 chars):', message.content?.substring(0, 100));
            console.log('Content length:', message.content?.length);
            console.log('Has AES key:', !!this.encryption.aesKey);
            
            let decryptedContent = message.content;
            
            // Always try to decrypt if we have an AES key and message looks encrypted
            if (this.encryption.aesKey) {
                try {
                    console.log('Attempting to decrypt message with AES...');
                    decryptedContent = await this.encryption.decryptWithAES(message.content);
                    console.log('Message decrypted successfully. Length:', decryptedContent?.length);
                    console.log('Decrypted content (first 100 chars):', decryptedContent?.substring(0, 100));
                } catch (decryptError) {
                    console.error('Decryption failed:', decryptError);
                    console.error('Decryption error details:', {
                        message: decryptError.message,
                        name: decryptError.name,
                        stack: decryptError.stack
                    });
                    // Show error but still try to display
                    decryptedContent = `[Decryption Error: ${decryptError.message}]`;
                    if (window.app) {
                        window.app.showNotification('Failed to decrypt message. Please reconnect.', 'error');
                    }
                }
            } else {
                console.warn('No AES key available. Message will be displayed as encrypted.');
                decryptedContent = `[No decryption key available] ${message.content}`;
            }
            
            // Add to UI
            this.addMessageToUI({
                id: message.id,
                senderId: message.senderId,
                content: decryptedContent,
                messageType: message.messageType,
                timestamp: message.timestamp,
                isOwn: message.senderId === this.currentUser.id
            });
            
        } catch (error) {
            console.error('Failed to handle received message:', error);
            console.error('Error details:', {
                message: error.message,
                stack: error.stack,
                name: error.name,
                contentLength: message?.content?.length,
                hasAESKey: !!this.encryption?.aesKey
            });
            if (window.app) {
                window.app.showNotification('Failed to process message: ' + error.message, 'error');
            }
            
            // Still show the message for debugging
            this.addMessageToUI({
                id: message?.id || Date.now().toString(),
                senderId: message?.senderId || 'unknown',
                content: `[Error processing message: ${error.message}]`,
                messageType: message?.messageType || 'text',
                timestamp: message?.timestamp || new Date().toISOString(),
                isOwn: message?.senderId === this.currentUser?.id
            });
        }
    }

    handleUserOnline(user) {
        console.log('User online:', user);
        if (window.app) {
            window.app.onUserOnline(user);
        }
    }

    handleUserOffline(user) {
        console.log('User offline:', user);
        if (window.app) {
            window.app.onUserOffline(user);
        }
    }

    handleOnlineUsers(users) {
        console.log('Online users updated:', users);
        if (window.app) {
            window.app.updateOnlineUsers();
        }
    }

    async handleMessageHistory(messages) {
        console.log('Message history received:', messages);
        console.log('Messages count:', messages.length);
        console.log('Has AES key:', !!this.encryption.aesKey);
        
        // Clear existing messages
        document.getElementById('messages').innerHTML = '';
        
        // Process messages sequentially to decrypt each one
        for (const message of messages) {
            try {
                console.log('Processing message:', message.id, 'Content length:', message.content?.length);
                
                let decryptedContent = message.content;
                
                // Check if message is already an error message or too short
                const isErrorMessage = message.content.includes('[Unable to decrypt') || 
                                     message.content.includes('Decryption Error') ||
                                     message.content.includes('[Message too short');
                
                // Base64 decode to check actual byte length
                // 44 bytes base64 ≈ 33 bytes actual (too short for IV 16 + encrypted 16)
                // A valid encrypted message should be at least ~32 bytes base64 (for very short message)
                // But typically 44 bytes base64 = 33 bytes actual, which is too short
                let isTooShort = false;
                if (!isErrorMessage && message.content.length > 0) {
                    try {
                        // Approximate: base64 length is about 4/3 of actual bytes
                        const estimatedBytes = Math.floor(message.content.length * 3 / 4);
                        // Need at least 32 bytes (16 IV + 16 encrypted minimum)
                        if (estimatedBytes < 32) {
                            isTooShort = true;
                            console.warn(`Message ${message.id} is too short: ${message.content.length} base64 chars ≈ ${estimatedBytes} bytes`);
                        }
                    } catch (e) {
                        // Ignore
                    }
                }
                
                // Try to decrypt if we have an AES key and message is valid
                if (this.encryption.aesKey && !isErrorMessage && !isTooShort) {
                    try {
                        console.log('Attempting to decrypt message history item...');
                        decryptedContent = await this.encryption.decryptWithAES(message.content);
                        console.log('Message decrypted successfully. Length:', decryptedContent?.length);
                        console.log('Decrypted content (first 100 chars):', decryptedContent?.substring(0, 100));
                    } catch (decryptError) {
                        console.error('Failed to decrypt message history item:', decryptError);
                        console.error('Error details:', {
                            message: decryptError.message,
                            name: decryptError.name,
                            messageId: message.id,
                            contentLength: message.content?.length
                        });
                        
                        // Determine appropriate error message
                        if (message.content.length < 44) {
                            decryptedContent = '[Old message - unable to decrypt with current key]';
                        } else {
                            decryptedContent = `[Decryption Error: ${decryptError.message}]`;
                        }
                    }
                } else {
                    // Handle error messages or too short messages
                    if (isErrorMessage) {
                        decryptedContent = message.content; // Use server's error message
                    } else if (isTooShort) {
                        decryptedContent = '[Old message - encrypted data too short]';
                    } else if (!this.encryption.aesKey) {
                        console.warn('No AES key available for message history');
                        decryptedContent = '[No decryption key available]';
                    }
                }
                
                // Add to UI with decrypted content
                this.addMessageToUI({
                    id: message.id,
                    senderId: message.senderId,
                    content: decryptedContent,
                    messageType: message.messageType,
                    timestamp: message.timestamp,
                    isOwn: message.senderId === this.currentUser.id
                });
                
            } catch (error) {
                console.error('Error processing message history item:', error);
                // Still add the message with error indicator
                this.addMessageToUI({
                    id: message.id,
                    senderId: message.senderId,
                    content: `[Error processing message: ${error.message}]`,
                    messageType: message.messageType,
                    timestamp: message.timestamp,
                    isOwn: message.senderId === this.currentUser.id
                });
            }
        }
        
        console.log('Message history processing complete');
    }

    handleError(error) {
        console.error('Server error:', error);
        console.error('Error details:', {
            message: error?.message || error,
            type: typeof error,
            string: String(error),
            errorObject: error
        });
        
        let errorMessage = 'An error occurred';
        if (typeof error === 'string') {
            errorMessage = error;
        } else if (error?.message) {
            errorMessage = error.message;
        } else if (error) {
            errorMessage = String(error);
        }
        
        if (window.app) {
            window.app.showNotification(errorMessage, 'error');
        }
    }

    handleSuccess(message) {
        console.log('Server success:', message);
        if (window.app) {
            window.app.showNotification(message.message || 'Operation successful', 'success');
        }
    }

    addMessageToUI(message) {
        const messagesContainer = document.getElementById('messages');
        const messageElement = document.createElement('div');
        messageElement.className = `message ${message.isOwn ? 'own' : ''}`;
        
        const timestamp = new Date(message.timestamp).toLocaleTimeString();
        const senderName = message.isOwn ? 'You' : `User ${message.senderId}`;
        
        messageElement.innerHTML = `
            <div class="message-avatar">${senderName.charAt(0)}</div>
            <div class="message-content">
                <div class="message-header">
                    <span class="message-sender">${senderName}</span>
                    <span class="message-time">${timestamp}</span>
                </div>
                <div class="message-text">${this.escapeHtml(message.content)}</div>
                <div class="message-status">
                    <i class="fas fa-lock"></i> Encrypted
                </div>
            </div>
        `;
        
        messagesContainer.appendChild(messageElement);
        
        // Scroll to bottom
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    processMessageQueue() {
        console.log('Processing queued messages...');
        
        while (this.messageQueue.length > 0) {
            const queuedMessage = this.messageQueue.shift();
            this.sendMessage(queuedMessage.content, queuedMessage.receiverId);
        }
    }

    async disconnect() {
        try {
            if (this.connection && this.isConnected) {
                await this.connection.invoke("LeaveChat", this.currentUser.id);
                await this.connection.stop();
            }
            
            this.isConnected = false;
            this.currentUser = null;
            this.messageQueue = [];
            
            console.log('Disconnected from chat');
        } catch (error) {
            console.error('Error during disconnect:', error);
        }
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Public methods
    async getMessageHistory(otherUserId = null, limit = 50) {
        try {
            if (this.connection && this.isConnected) {
                await this.connection.invoke("GetMessageHistory", this.currentUser.id, otherUserId, limit);
            }
        } catch (error) {
            console.error('Failed to get message history:', error);
        }
    }

    async getOnlineUsers() {
        try {
            if (this.connection && this.isConnected) {
                await this.connection.invoke("GetOnlineUsers");
            }
        } catch (error) {
            console.error('Failed to get online users:', error);
        }
    }
}
