using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLanChat.Data;
using System.Net.Http.Json;
using Xunit;

namespace SecureLanChat.Tests.IntegrationTests
{
    public class UserApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly ChatDbContext _context;

        public UserApiTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ChatDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    // Add in-memory database
                    services.AddDbContext<ChatDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("TestDatabase");
                    });
                });
            });

            _client = _factory.CreateClient();
            _context = _factory.Services.GetRequiredService<ChatDbContext>();
        }

        [Fact]
        public async Task Register_ShouldReturnSuccess_WithValidData()
        {
            // Arrange
            var request = new
            {
                Username = "testuser",
                Password = "password123",
                Email = "test@example.com"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/register", request);

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("testuser", result.User?.Username);
        }

        [Fact]
        public async Task Register_ShouldReturnConflict_WithExistingUsername()
        {
            // Arrange
            var request = new
            {
                Username = "existinguser",
                Password = "password123",
                Email = "test@example.com"
            };

            // Register first time
            await _client.PostAsJsonAsync("/api/auth/register", request);

            // Act - Try to register again
            var response = await _client.PostAsJsonAsync("/api/auth/register", request);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("already exists", result.Message);
        }

        [Fact]
        public async Task Login_ShouldReturnSuccess_WithValidCredentials()
        {
            // Arrange
            var registerRequest = new
            {
                Username = "testuser",
                Password = "password123",
                Email = "test@example.com"
            };

            // Register user first
            await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

            var loginRequest = new
            {
                Username = "testuser",
                Password = "password123"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal("testuser", result.User?.Username);
            Assert.NotNull(result.PublicKey);
            Assert.NotNull(result.PrivateKey);
            Assert.NotNull(result.AESKey);
        }

        [Fact]
        public async Task Login_ShouldReturnUnauthorized_WithInvalidCredentials()
        {
            // Arrange
            var loginRequest = new
            {
                Username = "nonexistent",
                Password = "wrongpassword"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Invalid", result.Message);
        }

        [Fact]
        public async Task Logout_ShouldReturnSuccess_WithValidUser()
        {
            // Arrange
            var registerRequest = new
            {
                Username = "testuser",
                Password = "password123",
                Email = "test@example.com"
            };

            // Register and login
            await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { Username = "testuser", Password = "password123" });
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

            var logoutRequest = new
            {
                UserId = loginResult?.User?.Id
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/logout", logoutRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<LogoutResponse>();
            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task GetOnlineUsers_ShouldReturnOnlineUsers()
        {
            // Arrange
            var registerRequest = new
            {
                Username = "testuser",
                Password = "password123",
                Email = "test@example.com"
            };

            // Register and login
            await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
            await _client.PostAsJsonAsync("/api/auth/login", new { Username = "testuser", Password = "password123" });

            // Act
            var response = await _client.GetAsync("/api/user/online");

            // Assert
            response.EnsureSuccessStatusCode();
            var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
            Assert.NotNull(users);
            Assert.Single(users);
            Assert.Equal("testuser", users[0].Username);
            Assert.True(users[0].IsOnline);
        }

        [Fact]
        public async Task GetUser_ShouldReturnUser_WithValidId()
        {
            // Arrange
            var registerRequest = new
            {
                Username = "testuser",
                Password = "password123",
                Email = "test@example.com"
            };

            // Register user
            var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

            // Act
            var response = await _client.GetAsync($"/api/user/{registerResult?.User?.Id}");

            // Assert
            response.EnsureSuccessStatusCode();
            var user = await response.Content.ReadFromJsonAsync<UserDto>();
            Assert.NotNull(user);
            Assert.Equal("testuser", user.Username);
        }

        [Fact]
        public async Task GetUser_ShouldReturnNotFound_WithInvalidId()
        {
            // Arrange
            var invalidUserId = Guid.NewGuid().ToString();

            // Act
            var response = await _client.GetAsync($"/api/user/{invalidUserId}");

            // Assert
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateUserStatus_ShouldUpdateStatus_WithValidUser()
        {
            // Arrange
            var registerRequest = new
            {
                Username = "testuser",
                Password = "password123",
                Email = "test@example.com"
            };

            // Register user
            var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

            var updateRequest = new
            {
                IsOnline = true
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/user/{registerResult?.User?.Id}/status", updateRequest);

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<dynamic>();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetUserStats_ShouldReturnStatistics()
        {
            // Arrange
            var registerRequest = new
            {
                Username = "testuser",
                Password = "password123",
                Email = "test@example.com"
            };

            // Register and login user
            await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
            await _client.PostAsJsonAsync("/api/auth/login", new { Username = "testuser", Password = "password123" });

            // Act
            var response = await _client.GetAsync("/api/user/stats");

            // Assert
            response.EnsureSuccessStatusCode();
            var stats = await response.Content.ReadFromJsonAsync<UserStatsResponse>();
            Assert.NotNull(stats);
            Assert.Equal(1, stats.TotalUsers);
            Assert.Equal(1, stats.OnlineUsers);
            Assert.Equal(0, stats.OfflineUsers);
        }

        [Fact]
        public async Task ValidateUser_ShouldReturnTrue_WithValidUser()
        {
            // Arrange
            var registerRequest = new
            {
                Username = "testuser",
                Password = "password123",
                Email = "test@example.com"
            };

            // Register user
            var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

            // Act
            var response = await _client.GetAsync($"/api/user/{registerResult?.User?.Id}/validate");

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ValidationResponse>();
            Assert.NotNull(result);
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task ValidateUser_ShouldReturnFalse_WithInvalidUser()
        {
            // Arrange
            var invalidUserId = Guid.NewGuid().ToString();

            // Act
            var response = await _client.GetAsync($"/api/user/{invalidUserId}/validate");

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ValidationResponse>();
            Assert.NotNull(result);
            Assert.False(result.IsValid);
        }

        public void Dispose()
        {
            _context.Dispose();
            _client.Dispose();
        }
    }

    // Response Models for testing
    public class RegisterResponse
    {
        public bool Success { get; set; }
        public UserDto? User { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public UserDto? User { get; set; }
        public string? PublicKey { get; set; }
        public string? PrivateKey { get; set; }
        public string? AESKey { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class LogoutResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ValidationResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class UserStatsResponse
    {
        public int TotalUsers { get; set; }
        public int OnlineUsers { get; set; }
        public int OfflineUsers { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
