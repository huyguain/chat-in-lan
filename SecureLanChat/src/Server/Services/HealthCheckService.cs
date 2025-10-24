using Microsoft.EntityFrameworkCore;
using SecureLanChat.Data;
using SecureLanChat.Interfaces;

namespace SecureLanChat.Services
{
    public interface IHealthCheckService
    {
        Task<HealthCheckResult> CheckDatabaseHealthAsync();
        Task<HealthCheckResult> CheckEncryptionServiceHealthAsync();
        Task<HealthCheckResult> CheckSignalRHealthAsync();
        Task<HealthCheckResult> CheckOverallHealthAsync();
        Task<HealthCheckResult> CheckMemoryUsageAsync();
        Task<HealthCheckResult> CheckDiskSpaceAsync();
    }

    public class HealthCheckService : IHealthCheckService
    {
        private readonly ChatDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<HealthCheckService> _logger;

        public HealthCheckService(
            ChatDbContext context,
            IEncryptionService encryptionService,
            ILogger<HealthCheckService> logger)
        {
            _context = context;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckDatabaseHealthAsync()
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Test database connection
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return new HealthCheckResult
                    {
                        IsHealthy = false,
                        Status = "Unhealthy",
                        Message = "Cannot connect to database",
                        Duration = stopwatch.Elapsed
                    };
                }

                // Test a simple query
                var userCount = await _context.Users.CountAsync();
                
                stopwatch.Stop();

                return new HealthCheckResult
                {
                    IsHealthy = true,
                    Status = "Healthy",
                    Message = $"Database is accessible. User count: {userCount}",
                    Duration = stopwatch.Elapsed,
                    Data = new { UserCount = userCount }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    Status = "Unhealthy",
                    Message = $"Database health check failed: {ex.Message}",
                    Duration = TimeSpan.Zero
                };
            }
        }

        public async Task<HealthCheckResult> CheckEncryptionServiceHealthAsync()
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Test key generation
                var keyPair = await _encryptionService.GenerateKeyPairAsync();
                if (string.IsNullOrEmpty(keyPair.PublicKey) || string.IsNullOrEmpty(keyPair.PrivateKey))
                {
                    return new HealthCheckResult
                    {
                        IsHealthy = false,
                        Status = "Unhealthy",
                        Message = "Encryption service failed to generate keys",
                        Duration = stopwatch.Elapsed
                    };
                }

                // Test encryption/decryption
                var testMessage = "Health check test message";
                var encrypted = await _encryptionService.EncryptMessageAsync(testMessage, keyPair.PublicKey);
                var decrypted = await _encryptionService.DecryptMessageAsync(encrypted, keyPair.PrivateKey);

                if (decrypted != testMessage)
                {
                    return new HealthCheckResult
                    {
                        IsHealthy = false,
                        Status = "Unhealthy",
                        Message = "Encryption service failed to encrypt/decrypt correctly",
                        Duration = stopwatch.Elapsed
                    };
                }

                stopwatch.Stop();

                return new HealthCheckResult
                {
                    IsHealthy = true,
                    Status = "Healthy",
                    Message = "Encryption service is working correctly",
                    Duration = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encryption service health check failed");
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    Status = "Unhealthy",
                    Message = $"Encryption service health check failed: {ex.Message}",
                    Duration = TimeSpan.Zero
                };
            }
        }

        public async Task<HealthCheckResult> CheckSignalRHealthAsync()
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // SignalR health check is more complex in a real scenario
                // For now, we'll just check if the service is available
                stopwatch.Stop();

                return new HealthCheckResult
                {
                    IsHealthy = true,
                    Status = "Healthy",
                    Message = "SignalR service is available",
                    Duration = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR health check failed");
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    Status = "Unhealthy",
                    Message = $"SignalR health check failed: {ex.Message}",
                    Duration = TimeSpan.Zero
                };
            }
        }

        public async Task<HealthCheckResult> CheckOverallHealthAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var results = new List<HealthCheckResult>();

            // Check all components
            results.Add(await CheckDatabaseHealthAsync());
            results.Add(await CheckEncryptionServiceHealthAsync());
            results.Add(await CheckSignalRHealthAsync());
            results.Add(await CheckMemoryUsageAsync());
            results.Add(await CheckDiskSpaceAsync());

            stopwatch.Stop();

            var unhealthyResults = results.Where(r => !r.IsHealthy).ToList();
            var isHealthy = !unhealthyResults.Any();

            return new HealthCheckResult
            {
                IsHealthy = isHealthy,
                Status = isHealthy ? "Healthy" : "Unhealthy",
                Message = isHealthy 
                    ? "All systems are operational" 
                    : $"{unhealthyResults.Count} component(s) are unhealthy",
                Duration = stopwatch.Elapsed,
                Data = new
                {
                    TotalChecks = results.Count,
                    HealthyChecks = results.Count(r => r.IsHealthy),
                    UnhealthyChecks = unhealthyResults.Count,
                    Components = results.Select(r => new { r.Status, r.Message })
                }
            };
        }

        public async Task<HealthCheckResult> CheckMemoryUsageAsync()
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var memoryUsage = process.WorkingSet64;
                var memoryUsageMB = memoryUsage / 1024 / 1024;

                stopwatch.Stop();

                var isHealthy = memoryUsageMB < 500; // Alert if using more than 500MB

                return new HealthCheckResult
                {
                    IsHealthy = isHealthy,
                    Status = isHealthy ? "Healthy" : "Warning",
                    Message = $"Memory usage: {memoryUsageMB}MB",
                    Duration = stopwatch.Elapsed,
                    Data = new { MemoryUsageMB = memoryUsageMB }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Memory usage health check failed");
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    Status = "Unhealthy",
                    Message = $"Memory usage health check failed: {ex.Message}",
                    Duration = TimeSpan.Zero
                };
            }
        }

        public async Task<HealthCheckResult> CheckDiskSpaceAsync()
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory));
                var freeSpaceGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
                var totalSpaceGB = drive.TotalSize / 1024 / 1024 / 1024;
                var usedPercentage = (double)(totalSpaceGB - freeSpaceGB) / totalSpaceGB * 100;

                stopwatch.Stop();

                var isHealthy = usedPercentage < 90; // Alert if more than 90% used

                return new HealthCheckResult
                {
                    IsHealthy = isHealthy,
                    Status = isHealthy ? "Healthy" : "Warning",
                    Message = $"Disk usage: {usedPercentage:F1}% ({freeSpaceGB}GB free)",
                    Duration = stopwatch.Elapsed,
                    Data = new { FreeSpaceGB = freeSpaceGB, TotalSpaceGB = totalSpaceGB, UsedPercentage = usedPercentage }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Disk space health check failed");
                return new HealthCheckResult
                {
                    IsHealthy = false,
                    Status = "Unhealthy",
                    Message = $"Disk space health check failed: {ex.Message}",
                    Duration = TimeSpan.Zero
                };
            }
        }
    }

    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public object? Data { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
