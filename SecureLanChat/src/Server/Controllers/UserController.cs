using Microsoft.AspNetCore.Mvc;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace SecureLanChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<UserDto>>> GetAllUsers()
        {
            try
            {
                _logger.LogDebug("Retrieving all users");

                var users = await _userService.GetAllUsersAsync();
                var userDtos = users.Select(u => new UserDto
                {
                    Id = u.Id.ToString(),
                    Username = u.Username,
                    Email = u.Email,
                    IsOnline = u.IsOnline,
                    LastSeen = u.LastSeen,
                    CreatedAt = u.CreatedAt
                }).ToList();

                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve all users");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("online")]
        public async Task<ActionResult<List<UserDto>>> GetOnlineUsers()
        {
            try
            {
                _logger.LogDebug("Retrieving online users");

                var users = await _userService.GetOnlineUsersAsync();
                var userDtos = users.Select(u => new UserDto
                {
                    Id = u.Id.ToString(),
                    Username = u.Username,
                    Email = u.Email,
                    IsOnline = u.IsOnline,
                    LastSeen = u.LastSeen,
                    CreatedAt = u.CreatedAt
                }).ToList();

                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve online users");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult<UserDto>> GetUser(string userId)
        {
            try
            {
                _logger.LogDebug("Retrieving user {UserId}", userId);

                var user = await _userService.GetUserByIdAsync(userId);
                var userDto = new UserDto
                {
                    Id = user.Id.ToString(),
                    Username = user.Username,
                    Email = user.Email,
                    IsOnline = user.IsOnline,
                    LastSeen = user.LastSeen,
                    CreatedAt = user.CreatedAt
                };

                return Ok(userDto);
            }
            catch (UserNotFoundException)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return NotFound("User not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("username/{username}")]
        public async Task<ActionResult<UserDto>> GetUserByUsername(string username)
        {
            try
            {
                _logger.LogDebug("Retrieving user by username {Username}", username);

                var user = await _userService.GetUserByUsernameAsync(username);
                var userDto = new UserDto
                {
                    Id = user.Id.ToString(),
                    Username = user.Username,
                    Email = user.Email,
                    IsOnline = user.IsOnline,
                    LastSeen = user.LastSeen,
                    CreatedAt = user.CreatedAt
                };

                return Ok(userDto);
            }
            catch (UserNotFoundException)
            {
                _logger.LogWarning("User with username {Username} not found", username);
                return NotFound("User not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve user by username {Username}", username);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{userId}/status")]
        public async Task<ActionResult> UpdateUserStatus(string userId, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Updating status for user {UserId} to {Status}", userId, request.IsOnline);

                await _userService.UpdateUserStatusAsync(userId, request.IsOnline);

                return Ok(new { Success = true, Message = "Status updated successfully" });
            }
            catch (UserNotFoundException)
            {
                _logger.LogWarning("Status update failed: User {UserId} not found", userId);
                return NotFound("User not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update status for user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{userId}/lastseen")]
        public async Task<ActionResult> UpdateLastSeen(string userId)
        {
            try
            {
                _logger.LogDebug("Updating last seen for user {UserId}", userId);

                await _userService.UpdateLastSeenAsync(userId);

                return Ok(new { Success = true, Message = "Last seen updated successfully" });
            }
            catch (UserNotFoundException)
            {
                _logger.LogWarning("Last seen update failed: User {UserId} not found", userId);
                return NotFound("User not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update last seen for user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{userId}/validate")]
        public async Task<ActionResult<ValidationResponse>> ValidateUser(string userId)
        {
            try
            {
                _logger.LogDebug("Validating user {UserId}", userId);

                var isValid = await _userService.ValidateUserAsync(userId);

                var response = new ValidationResponse
                {
                    IsValid = isValid,
                    Message = isValid ? "User is valid" : "User not found"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{userId}/online")]
        public async Task<ActionResult<OnlineStatusResponse>> GetUserOnlineStatus(string userId)
        {
            try
            {
                _logger.LogDebug("Checking online status for user {UserId}", userId);

                var isOnline = await _userService.IsUserOnlineAsync(userId);

                var response = new OnlineStatusResponse
                {
                    IsOnline = isOnline,
                    Message = isOnline ? "User is online" : "User is offline"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check online status for user {UserId}", userId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("stats")]
        public async Task<ActionResult<UserStatsResponse>> GetUserStats()
        {
            try
            {
                _logger.LogDebug("Retrieving user statistics");

                var allUsers = await _userService.GetAllUsersAsync();
                var onlineUsers = await _userService.GetOnlineUsersAsync();

                var stats = new UserStatsResponse
                {
                    TotalUsers = allUsers.Count,
                    OnlineUsers = onlineUsers.Count,
                    OfflineUsers = allUsers.Count - onlineUsers.Count,
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve user statistics");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    // Request/Response Models
    public class ValidationResponse
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class OnlineStatusResponse
    {
        public bool IsOnline { get; set; }
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
