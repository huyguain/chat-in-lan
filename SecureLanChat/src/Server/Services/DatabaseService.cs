using Microsoft.EntityFrameworkCore;
using SecureLanChat.Data;
using SecureLanChat.Interfaces;

namespace SecureLanChat.Services
{
    public interface IDatabaseService
    {
        Task<bool> IsDatabaseConnectedAsync();
        Task<bool> TestConnectionAsync();
        Task<int> GetConnectionCountAsync();
        Task CleanupExpiredSessionsAsync();
        Task OptimizeDatabaseAsync();
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService(ChatDbContext context, ILogger<DatabaseService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> IsDatabaseConnectedAsync()
        {
            try
            {
                return await _context.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check database connection");
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                // Simple query to test connection
                await _context.Users.CountAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection test failed");
                return false;
            }
        }

        public async Task<int> GetConnectionCountAsync()
        {
            try
            {
                // This is a simplified implementation
                // In a real scenario, you might want to use performance counters
                return await _context.Sessions.CountAsync(s => s.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get connection count");
                return 0;
            }
        }

        public async Task CleanupExpiredSessionsAsync()
        {
            try
            {
                var expiredSessions = await _context.Sessions
                    .Where(s => s.ExpiresAt < DateTime.UtcNow)
                    .ToListAsync();

                if (expiredSessions.Any())
                {
                    _context.Sessions.RemoveRange(expiredSessions);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup expired sessions");
            }
        }

        public async Task OptimizeDatabaseAsync()
        {
            try
            {
                // Update statistics for better query performance
                await _context.Database.ExecuteSqlRawAsync("UPDATE STATISTICS Users");
                await _context.Database.ExecuteSqlRawAsync("UPDATE STATISTICS Messages");
                await _context.Database.ExecuteSqlRawAsync("UPDATE STATISTICS Sessions");
                
                _logger.LogInformation("Database optimization completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize database");
            }
        }
    }
}
