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
        
        this.init();
    }

    async init() {
        try {
            console.log('Initializing Secure Chat Application...');
            
            // Initialize encryption module
            this.encryption = new ClientEncryption();
            await this.encryption.initialize();
            
            // Initialize chat module
            this.chat = new ChatManager(this.encryption);
            
            // Setup event listeners
            this.setupEventListeners();
            
            // Check for existing session
            await this.checkExistingSession();
            
            console.log('Application initialized successfully');
        } catch (error) {
            console.error('Failed to initialize application:', error);
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
                this.currentUser = JSON.parse(savedUser);
                await this.connectToChat();
            } else {
                this.showAuthModal();
            }
        } catch (error) {
            console.error('Error checking existing session:', error);
            this.showAuthModal();
        }
    }

    showAuthModal() {
        document.getElementById('authModal').classList.remove('hidden');
        document.getElementById('chatInterface').classList.add('hidden');
    }

    hideAuthModal() {
        document.getElementById('authModal').classList.add('hidden');
        document.getElementById('chatInterface').classList.remove('hidden');
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
            this.showLoading('Connecting to secure chat...');
            
            // Update UI
            document.getElementById('currentUser').textContent = this.currentUser.username;
            this.updateConnectionStatus('connecting');
            
            // Connect to chat
            await this.chat.connect(this.currentUser.id);
            
            this.isConnected = true;
            this.updateConnectionStatus('online');
            this.hideLoading();
            
            this.showNotification('Connected to secure chat!', 'success');
        } catch (error) {
            console.error('Connection error:', error);
            this.hideLoading();
            this.updateConnectionStatus('offline');
            this.showNotification('Failed to connect to chat', 'error');
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
            
            // Clear input
            messageInput.value = '';
            
            // Send message through chat manager
            await this.chat.sendMessage(message);
            
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
        document.getElementById('loadingOverlay').classList.add('hidden');
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
            
            users.forEach(user => {
                const userElement = document.createElement('div');
                userElement.className = 'user-item online';
                userElement.innerHTML = `
                    <div class="user-avatar">${user.username.charAt(0).toUpperCase()}</div>
                    <div class="user-name">${user.username}</div>
                `;
                onlineUsersContainer.appendChild(userElement);
            });
        } catch (error) {
            console.error('Failed to update online users:', error);
        }
    }
}

// Initialize application when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.app = new SecureChatApp();
});
