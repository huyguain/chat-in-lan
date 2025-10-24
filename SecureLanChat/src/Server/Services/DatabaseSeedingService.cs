using Microsoft.EntityFrameworkCore;
using SecureLanChat.Data;
using SecureLanChat.Models;
using System.Security.Cryptography;
using System.Text;

namespace SecureLanChat.Services
{
    public interface IDatabaseSeedingService
    {
        Task SeedAsync();
        Task SeedTestUsersAsync();
        Task SeedTestMessagesAsync();
        Task ClearAllDataAsync();
    }

    public class DatabaseSeedingService : IDatabaseSeedingService
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<DatabaseSeedingService> _logger;

        public DatabaseSeedingService(ChatDbContext context, ILogger<DatabaseSeedingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                // Check if database is already seeded
                if (await _context.Users.AnyAsync())
                {
                    _logger.LogInformation("Database already seeded, skipping seeding process");
                    return;
                }

                _logger.LogInformation("Starting database seeding...");

                await SeedTestUsersAsync();
                await SeedTestMessagesAsync();

                _logger.LogInformation("Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed database");
                throw;
            }
        }

        public async Task SeedTestUsersAsync()
        {
            var testUsers = new List<User>
            {
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "admin",
                    PublicKey = GenerateTestPublicKey(),
                    IsOnline = false,
                    LastSeen = DateTime.UtcNow.AddDays(-1),
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "user1",
                    PublicKey = GenerateTestPublicKey(),
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow.AddMinutes(-5),
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "user2",
                    PublicKey = GenerateTestPublicKey(),
                    IsOnline = true,
                    LastSeen = DateTime.UtcNow.AddMinutes(-2),
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                },
                new User
                {
                    Id = Guid.NewGuid(),
                    Username = "testuser",
                    PublicKey = GenerateTestPublicKey(),
                    IsOnline = false,
                    LastSeen = DateTime.UtcNow.AddHours(-2),
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            _context.Users.AddRange(testUsers);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} test users", testUsers.Count);
        }

        public async Task SeedTestMessagesAsync()
        {
            var users = await _context.Users.ToListAsync();
            if (users.Count < 2) return;

            var admin = users.First(u => u.Username == "admin");
            var user1 = users.First(u => u.Username == "user1");
            var user2 = users.First(u => u.Username == "user2");

            var testMessages = new List<Message>
            {
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = admin.Id,
                    ReceiverId = null, // Broadcast
                    Content = "Chào mừng mọi người đến với hệ thống chat mã hóa!",
                    IV = GenerateRandomIV(),
                    MessageType = MessageType.Broadcast,
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = user1.Id,
                    ReceiverId = user2.Id,
                    Content = "Xin chào user2!",
                    IV = GenerateRandomIV(),
                    MessageType = MessageType.Private,
                    CreatedAt = DateTime.UtcNow.AddHours(-1)
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = user2.Id,
                    ReceiverId = user1.Id,
                    Content = "Chào user1! Bạn khỏe không?",
                    IV = GenerateRandomIV(),
                    MessageType = MessageType.Private,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-30)
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    SenderId = admin.Id,
                    ReceiverId = null, // Broadcast
                    Content = "Hệ thống đã được cập nhật với tính năng mã hóa mới!",
                    IV = GenerateRandomIV(),
                    MessageType = MessageType.Broadcast,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-15)
                }
            };

            _context.Messages.AddRange(testMessages);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Seeded {Count} test messages", testMessages.Count);
        }

        public async Task ClearAllDataAsync()
        {
            try
            {
                _logger.LogWarning("Clearing all data from database...");

                _context.Sessions.RemoveRange(_context.Sessions);
                _context.Messages.RemoveRange(_context.Messages);
                _context.Users.RemoveRange(_context.Users);

                await _context.SaveChangesAsync();

                _logger.LogInformation("All data cleared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear data");
                throw;
            }
        }

        private string GenerateTestPublicKey()
        {
            // Generate a test RSA public key (simplified)
            using var rsa = RSA.Create(2048);
            return Convert.ToBase64String(rsa.ExportRSAPublicKey());
        }

        private string GenerateRandomIV()
        {
            var iv = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(iv);
            return Convert.ToBase64String(iv);
        }
    }
}
