using Microsoft.AspNetCore.Mvc;
using SecureLanChat.Services;

namespace SecureLanChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IHealthCheckService _healthCheckService;
        private readonly ILogger<HealthController> _logger;

        public HealthController(IHealthCheckService healthCheckService, ILogger<HealthController> logger)
        {
            _healthCheckService = healthCheckService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var result = await _healthCheckService.CheckOverallHealthAsync();
                
                var statusCode = result.IsHealthy ? 200 : 503;
                
                return StatusCode(statusCode, new
                {
                    Status = result.Status,
                    Message = result.Message,
                    Duration = result.Duration.TotalMilliseconds,
                    Timestamp = result.Timestamp,
                    Data = result.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(503, new
                {
                    Status = "Unhealthy",
                    Message = "Health check failed",
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        [HttpGet("database")]
        public async Task<IActionResult> GetDatabaseHealth()
        {
            try
            {
                var result = await _healthCheckService.CheckDatabaseHealthAsync();
                var statusCode = result.IsHealthy ? 200 : 503;
                
                return StatusCode(statusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return StatusCode(503, new { Error = ex.Message });
            }
        }

        [HttpGet("encryption")]
        public async Task<IActionResult> GetEncryptionHealth()
        {
            try
            {
                var result = await _healthCheckService.CheckEncryptionServiceHealthAsync();
                var statusCode = result.IsHealthy ? 200 : 503;
                
                return StatusCode(statusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encryption health check failed");
                return StatusCode(503, new { Error = ex.Message });
            }
        }

        [HttpGet("signalr")]
        public async Task<IActionResult> GetSignalRHealth()
        {
            try
            {
                var result = await _healthCheckService.CheckSignalRHealthAsync();
                var statusCode = result.IsHealthy ? 200 : 503;
                
                return StatusCode(statusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SignalR health check failed");
                return StatusCode(503, new { Error = ex.Message });
            }
        }

        [HttpGet("memory")]
        public async Task<IActionResult> GetMemoryHealth()
        {
            try
            {
                var result = await _healthCheckService.CheckMemoryUsageAsync();
                var statusCode = result.IsHealthy ? 200 : 503;
                
                return StatusCode(statusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Memory health check failed");
                return StatusCode(503, new { Error = ex.Message });
            }
        }

        [HttpGet("disk")]
        public async Task<IActionResult> GetDiskHealth()
        {
            try
            {
                var result = await _healthCheckService.CheckDiskSpaceAsync();
                var statusCode = result.IsHealthy ? 200 : 503;
                
                return StatusCode(statusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Disk health check failed");
                return StatusCode(503, new { Error = ex.Message });
            }
        }
    }
}