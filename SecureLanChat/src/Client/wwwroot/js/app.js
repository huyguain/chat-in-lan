/**
 * Main Application Controller
 * Manages the overall application state and coordinates between modules
 */
class SecureChatApp {
    constructor() {
        this.currentUser = null;
        this.encryption = null;
        this.chat = null;
        this.isConnected = false;
        this.isEncrypted = false;
        this.selectedChatUser = null; // Selected user for private chat
        
        this.init();
    }

    async init() {
        try {
            console.log('Initializing Secure Chat Application...');
            
            // Initialize encryption module
            console.log('Initializing encryption...');
            this.encryption = new ClientEncryption();
            await this.encryption.initialize();
            console.log('Encryption initialized');
            
            // Initialize chat module
            console.log('Initializing chat manager...');
            this.chat = new ChatManager(this.encryption);
            console.log('Chat manager initialized');
            
            // Setup event listeners
            console.log('Setting up event listeners...');
            this.setupEventListeners();
            console.log('Event listeners setup complete');
            
            // Check for existing session
            console.log('Checking for existing session...');
            await this.checkExistingSession();
            console.log('Session check complete');
            
            // Ensure loading is hidden after initialization
            this.hideLoading();
            
            console.log('Application initialized successfully');
        } catch (error) {
            console.error('Failed to initialize application:', error);
            console.error('Error stack:', error.stack);
            this.hideLoading();
            this.showNotification('Failed to initialize application', 'error');
        }
    }

    setupEventListeners() {
        // Authentication events
        document.getElementById('loginFormElement').addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleLogin(e);
        });

        document.getElementById('registerFormElement').addEventListener('submit', (e) => {
            e.preventDefault();
            this.handleRegister(e);
        });

        // Tab switching
        document.querySelectorAll('.tab-button').forEach(button => {
            button.addEventListener('click', (e) => {
                this.switchTab(e.target.dataset.tab);
            });
        });

        // Chat events
        document.getElementById('logoutBtn').addEventListener('click', () => {
            this.handleLogout();
        });

        document.getElementById('sendBtn').addEventListener('click', () => {
            this.sendMessage();
        });

        document.getElementById('messageInput').addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                this.sendMessage();
            }
        });

        // Settings events
        document.getElementById('clearChatBtn').addEventListener('click', () => {
            this.clearChat();
        });

        document.getElementById('encryptionStatusBtn').addEventListener('click', () => {
            this.showEncryptionInfo();
        });

        // Toast close
        document.querySelector('.toast-close').addEventListener('click', () => {
            this.hideNotification();
        });
    }

    async checkExistingSession() {
        try {
            const savedUser = localStorage.getItem('currentUser');
            if (savedUser) {
                try {
                    this.currentUser = JSON.parse(savedUser);
                    console.log('Found saved user:', this.currentUser);
                    
                    // Validate user data
                    if (!this.currentUser || !this.currentUser.id) {
                        console.warn('Invalid saved user data. Clearing and showing login.');
                        localStorage.removeItem('currentUser');
                        this.hideLoading();
                        this.showAuthModal();
                        return;
                    }
                    
                    // Auto-connect (will handle loading internally)
                    await this.connectToChat();
                } catch (parseError) {
                    console.error('Error parsing saved user:', parseError);
                    localStorage.removeItem('currentUser');
                    this.hideLoading();
                    this.showAuthModal();
                }
            } else {
                console.log('No saved session found. Showing login modal.');
                this.hideLoading();
                this.showAuthModal();
            }
        } catch (error) {
            console.error('Error checking existing session:', error);
            this.hideLoading();
            this.showAuthModal();
        }
    }

    showAuthModal() {
        console.log('Showing auth modal');
        const authModal = document.getElementById('authModal');
        const chatInterface = document.getElementById('chatInterface');
        
        if (authModal) {
            authModal.classList.remove('hidden');
            console.log('Auth modal shown');
        } else {
            console.error('Auth modal element not found!');
        }
        
        if (chatInterface) {
            chatInterface.classList.add('hidden');
        }
    }

    hideAuthModal() {
        console.log('Hiding auth modal');
        const authModal = document.getElementById('authModal');
        const chatInterface = document.getElementById('chatInterface');
        
        if (authModal) {
            authModal.classList.add('hidden');
        }
        
        if (chatInterface) {
            chatInterface.classList.remove('hidden');
            console.log('Chat interface shown');
        }
    }

    switchTab(tabName) {
        // Update tab buttons
        document.querySelectorAll('.tab-button').forEach(button => {
            button.classList.remove('active');
        });
        document.querySelector(`[data-tab="${tabName}"]`).classList.add('active');

        // Update forms
        document.querySelectorAll('.auth-form').forEach(form => {
            form.classList.remove('active');
        });
        document.getElementById(`${tabName}Form`).classList.add('active');
    }

    async handleLogin(event) {
        try {
            this.showLoading('Logging in...');
            
            const formData = new FormData(event.target);
            const loginData = {
                username: formData.get('username'),
                password: formData.get('password')
            };

            const response = await fetch('/api/auth/login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(loginData)
            });

            const result = await response.json();

            if (result.success) {
                this.currentUser = result.user;
                localStorage.setItem('currentUser', JSON.stringify(this.currentUser));
                
                // Store encryption keys
                localStorage.setItem('publicKey', result.publicKey);
                localStorage.setItem('privateKey', result.privateKey);
                localStorage.setItem('aesKey', result.AESKey);
                
                this.hideLoading();
                this.hideAuthModal();
                await this.connectToChat();
                
                this.showNotification('Login successful!', 'success');
            } else {
                this.hideLoading();
                this.showNotification(result.message || 'Login failed', 'error');
            }
        } catch (error) {
            console.error('Login error:', error);
            this.hideLoading();
            this.showNotification('Login failed. Please try again.', 'error');
        }
    }

    async handleRegister(event) {
        try {
            this.showLoading('Creating account...');
            
            const formData = new FormData(event.target);
            const registerData = {
                username: formData.get('username'),
                password: formData.get('password'),
                email: formData.get('email') || null
            };

            const response = await fetch('/api/auth/register', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(registerData)
            });

            const result = await response.json();

            if (result.success) {
                this.hideLoading();
                this.showNotification('Registration successful! Please login.', 'success');
                this.switchTab('login');
            } else {
                this.hideLoading();
                this.showNotification(result.message || 'Registration failed', 'error');
            }
        } catch (error) {
            console.error('Registration error:', error);
            this.hideLoading();
            this.showNotification('Registration failed. Please try again.', 'error');
        }
    }

    async connectToChat() {
        try {
            // Validate user exists
            if (!this.currentUser || !this.currentUser.id) {
                console.error('No user logged in. Showing login modal.');
                this.showAuthModal();
                return;
            }

            console.log('Starting connection to chat for user:', this.currentUser.id);
            this.showLoading('Connecting to secure chat...');
            
            // Update UI
            const currentUserElement = document.getElementById('currentUser');
            if (currentUserElement) {
                currentUserElement.textContent = this.currentUser.username || 'User';
            }
            this.updateConnectionStatus('connecting');
            
            // Connect to chat
            console.log('Calling chat.connect with userId:', this.currentUser.id);
            await this.chat.connect(this.currentUser.id);
            
            this.isConnected = true;
            this.updateConnectionStatus('online');
            this.hideLoading();
            
            console.log('Successfully connected to chat!');
            this.showNotification('Connected to secure chat!', 'success');
        } catch (error) {
            console.error('Connection error:', error);
            console.error('Error stack:', error.stack);
            this.hideLoading();
            this.updateConnectionStatus('offline');
            
            let errorMessage = 'Failed to connect to chat';
            if (error.message) {
                errorMessage += ': ' + error.message;
            }
            this.showNotification(errorMessage, 'error');
        }
    }

    async handleLogout() {
        try {
            if (this.chat) {
                await this.chat.disconnect();
            }
            
            // Clear local storage
            localStorage.removeItem('currentUser');
            localStorage.removeItem('publicKey');
            localStorage.removeItem('privateKey');
            localStorage.removeItem('aesKey');
            
            // Reset state
            this.currentUser = null;
            this.isConnected = false;
            this.isEncrypted = false;
            
            // Show auth modal
            this.showAuthModal();
            
            this.showNotification('Logged out successfully', 'info');
        } catch (error) {
            console.error('Logout error:', error);
            this.showNotification('Logout failed', 'error');
        }
    }

    async sendMessage() {
        try {
            const messageInput = document.getElementById('messageInput');
            const message = messageInput.value.trim();
            
            if (!message) return;
            
            if (!this.isConnected) {
                this.showNotification('Not connected to chat', 'error');
                return;
            }
            
            // Get receiver ID if private chat is selected
            const receiverId = this.selectedChatUser ? this.selectedChatUser.id : null;
            
            // Clear input
            messageInput.value = '';
            
            // Send message through chat manager (with receiverId for private chat)
            await this.chat.sendMessage(message, receiverId);
            
        } catch (error) {
            console.error('Send message error:', error);
            this.showNotification('Failed to send message', 'error');
        }
    }

    clearChat() {
        if (confirm('Are you sure you want to clear the chat history?')) {
            document.getElementById('messages').innerHTML = '';
            this.showNotification('Chat cleared', 'info');
        }
    }

    showEncryptionInfo() {
        const status = this.encryption.getStatus();
        const info = `
            Encryption Status:
            • Initialized: ${status.isInitialized}
            • RSA Key Pair: ${status.hasRSAKeyPair}
            • AES Key: ${status.hasAESKey}
            • Server Public Key: ${status.hasServerPublicKey}
        `;
        alert(info);
    }

    updateConnectionStatus(status) {
        const statusElement = document.getElementById('connectionStatus');
        statusElement.textContent = status;
        statusElement.className = `status-indicator ${status}`;
    }

    showLoading(message = 'Loading...') {
        const overlay = document.getElementById('loadingOverlay');
        overlay.querySelector('p').textContent = message;
        overlay.classList.remove('hidden');
    }

    hideLoading() {
        const overlay = document.getElementById('loadingOverlay');
        if (overlay) {
            overlay.classList.add('hidden');
            console.log('Loading overlay hidden');
        }
    }

    showNotification(message, type = 'info') {
        const toast = document.getElementById('notificationToast');
        const icon = toast.querySelector('.toast-icon');
        const messageElement = toast.querySelector('.toast-message');
        
        // Set message
        messageElement.textContent = message;
        
        // Set icon based on type
        const icons = {
            success: 'fas fa-check-circle',
            error: 'fas fa-exclamation-circle',
            warning: 'fas fa-exclamation-triangle',
            info: 'fas fa-info-circle'
        };
        
        icon.className = `toast-icon ${icons[type] || icons.info}`;
        
        // Set toast class
        toast.className = `toast ${type}`;
        
        // Show toast
        toast.classList.remove('hidden');
        toast.classList.add('show');
        
        // Auto hide after 5 seconds
        setTimeout(() => {
            this.hideNotification();
        }, 5000);
    }

    hideNotification() {
        const toast = document.getElementById('notificationToast');
        toast.classList.remove('show');
        setTimeout(() => {
            toast.classList.add('hidden');
        }, 300);
    }

    // Public methods for chat manager to call
    onUserOnline(user) {
        this.updateOnlineUsers();
        this.showNotification(`${user.username} is now online`, 'info');
    }

    onUserOffline(user) {
        this.updateOnlineUsers();
        this.showNotification(`${user.username} is now offline`, 'info');
    }

    onMessageReceived(message) {
        // This will be handled by the chat manager
        console.log('Message received:', message);
    }

    async updateOnlineUsers() {
        try {
            const response = await fetch('/api/user/online');
            const users = await response.json();
            
            const onlineUsersContainer = document.getElementById('onlineUsers');
            onlineUsersContainer.innerHTML = '';
            
            // Add "All Users" option for broadcast
            const allUsersElement = document.createElement('div');
            allUsersElement.className = 'user-item online';
            allUsersElement.dataset.userId = '';
            allUsersElement.innerHTML = `
                <div class="user-avatar"><i class="fas fa-users"></i></div>
                <div class="user-name">All Users (Broadcast)</div>
            `;
            allUsersElement.addEventListener('click', () => this.selectChatUser(null));
            onlineUsersContainer.appendChild(allUsersElement);
            
            // Add separator
            const separator = document.createElement('div');
            separator.style.padding = '8px 12px';
            separator.style.color = '#999';
            separator.style.fontSize = '12px';
            separator.style.fontWeight = '600';
            separator.textContent = 'Online Users';
            onlineUsersContainer.appendChild(separator);
            
            users.forEach(user => {
                // Skip current user
                if (user.id === this.currentUser?.id) return;
                
                const userElement = document.createElement('div');
                userElement.className = 'user-item online';
                userElement.dataset.userId = user.id;
                userElement.innerHTML = `
                    <div class="user-avatar">${user.username.charAt(0).toUpperCase()}</div>
                    <div class="user-name">${user.username}</div>
                `;
                userElement.addEventListener('click', () => this.selectChatUser(user.id, user.username));
                onlineUsersContainer.appendChild(userElement);
            });
        } catch (error) {
            console.error('Failed to update online users:', error);
        }
    }

    selectChatUser(userId, username = null) {
        try {
            // Update selected user
            this.selectedChatUser = userId ? { id: userId, username: username } : null;
            
            // Update UI
            this.updateChatHeader();
            
            // Remove previous selection
            document.querySelectorAll('.user-item').forEach(item => {
                item.classList.remove('selected');
            });
            
            // Mark selected user
            const selectedElement = document.querySelector(`[data-user-id="${userId || ''}"]`);
            if (selectedElement) {
                selectedElement.classList.add('selected');
            }
            
            // Load message history for this conversation
            if (this.chat && userId) {
                this.chat.getMessageHistory(userId, 50);
            } else if (this.chat && !userId) {
                // Load broadcast messages
                this.chat.getMessageHistory(null, 50);
            }
            
            // Clear and reload messages
            this.clearMessages();
            
            console.log('Selected chat user:', this.selectedChatUser);
        } catch (error) {
            console.error('Failed to select chat user:', error);
        }
    }

    updateChatHeader() {
        const chatHeader = document.querySelector('.chat-header h1');
        if (chatHeader) {
            if (this.selectedChatUser) {
                chatHeader.innerHTML = `<i class="fas fa-user"></i> Chat with ${this.selectedChatUser.username}`;
            } else {
                chatHeader.innerHTML = `<i class="fas fa-shield-alt"></i> Secure LAN Chat (All Users)`;
            }
        }
    }

    clearMessages() {
        const messagesContainer = document.getElementById('messages');
        if (messagesContainer) {
            messagesContainer.innerHTML = '';
        }
    }
}

// Initialize application when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.app = new SecureChatApp();
});
