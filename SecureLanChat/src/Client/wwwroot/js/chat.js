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
                    transport: signalR.HttpTransportType.WebSockets,
                    skipNegotiation: true
                })
                .withAutomaticReconnect()
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Setup event handlers
            this.setupEventHandlers();

            // Start connection
            await this.connection.start();
            
            this.isConnected = true;
            this.currentUser = { id: userId };
            
            // Join chat
            await this.connection.invoke("JoinChat", userId);
            
            // Exchange encryption keys
            await this.exchangeKeys();
            
            console.log('Connected to SignalR hub successfully');
        } catch (error) {
            console.error('Failed to connect to SignalR hub:', error);
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
            const clientPublicKey = await this.encryption.exportPublicKey();
            
            // Exchange keys with server
            await this.connection.invoke("ExchangeKeys", this.currentUser.id, clientPublicKey);
            
        } catch (error) {
            console.error('Key exchange failed:', error);
            throw error;
        }
    }

    async handleKeysExchanged(keys) {
        try {
            console.log('Keys exchanged, importing server public key...');
            
            // Import server public key
            await this.encryption.importServerPublicKey(keys.serverPublicKey);
            
            // Import AES key
            const decryptedAESKey = await this.encryption.decryptAESKeyWithRSA(keys.encryptedAESKey);
            await this.encryption.importAESKey(decryptedAESKey);
            
            console.log('Encryption setup completed');
            
            // Process any queued messages
            this.processMessageQueue();
            
        } catch (error) {
            console.error('Failed to handle key exchange:', error);
            if (window.app) {
                window.app.showNotification('Encryption setup failed', 'error');
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
            
            // Encrypt message with AES
            const encryptedMessage = await this.encryption.encryptWithAES(content);
            
            // Send to server
            await this.connection.invoke("SendMessage", 
                this.currentUser.id, 
                receiverId, 
                encryptedMessage, 
                "text"
            );
            
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
            if (window.app) {
                window.app.showNotification('Failed to send message', 'error');
            }
        }
    }

    async handleMessageReceived(message) {
        try {
            console.log('Message received:', message);
            
            // Decrypt message
            const decryptedContent = await this.encryption.decryptWithAES(message.content);
            
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
            if (window.app) {
                window.app.showNotification('Failed to decrypt message', 'error');
            }
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

    handleMessageHistory(messages) {
        console.log('Message history received:', messages);
        
        // Clear existing messages
        document.getElementById('messages').innerHTML = '';
        
        // Add messages to UI
        messages.forEach(message => {
            this.addMessageToUI({
                id: message.id,
                senderId: message.senderId,
                content: message.content,
                messageType: message.messageType,
                timestamp: message.timestamp,
                isOwn: message.senderId === this.currentUser.id
            });
        });
    }

    handleError(error) {
        console.error('Server error:', error);
        if (window.app) {
            window.app.showNotification(error.message || 'An error occurred', 'error');
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
