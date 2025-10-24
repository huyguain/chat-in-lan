using Microsoft.AspNetCore.Mvc;
using SecureLanChat.Interfaces;
using SecureLanChat.Models;
using SecureLanChat.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace SecureLanChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IEncryptionService _encryptionService;
        private readonly IKeyStorageService _keyStorageService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IUserService userService,
            IEncryptionService encryptionService,
            IKeyStorageService keyStorageService,
            ILogger<AuthController> logger)
        {
            _userService = userService;
            _encryptionService = encryptionService;
            _keyStorageService = keyStorageService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Login attempt for user {Username}", request.Username);

                var user = await _userService.LoginAsync(request.Username, request.Password);

                // Generate encryption keys for the session
                var keyPair = await _encryptionService.GenerateKeyPairAsync();
                var aesKey = await _encryptionService.GenerateAESKeyAsync();

                // Store user's public key
                await _keyStorageService.StoreUserPublicKeyAsync(user.Id.ToString(), keyPair.PublicKey);

                var response = new LoginResponse
                {
                    Success = true,
                    User = new UserDto
                    {
                        Id = user.Id.ToString(),
                        Username = user.Username,
                        Email = user.Email,
                        IsOnline = user.IsOnline,
                        LastSeen = user.LastSeen
                    },
                    PublicKey = keyPair.PublicKey,
                    PrivateKey = keyPair.PrivateKey,
                    AESKey = aesKey,
                    Message = "Login successful"
                };

                _logger.LogInformation("User {Username} logged in successfully", request.Username);
                return Ok(response);
            }
            catch (UserNotFoundException)
            {
                _logger.LogWarning("Login failed: User {Username} not found", request.Username);
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    Message = "Invalid username or password"
                });
            }
            catch (InvalidCredentialsException)
            {
                _logger.LogWarning("Login failed: Invalid credentials for user {Username}", request.Username);
                return Unauthorized(new LoginResponse
                {
                    Success = false,
                    Message = "Invalid username or password"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for user {Username}", request.Username);
                return StatusCode(500, new LoginResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost("register")]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Registration attempt for user {Username}", request.Username);

                var user = await _userService.RegisterAsync(request.Username, request.Password, request.Email);

                var response = new RegisterResponse
                {
                    Success = true,
                    User = new UserDto
                    {
                        Id = user.Id.ToString(),
                        Username = user.Username,
                        Email = user.Email,
                        IsOnline = user.IsOnline,
                        LastSeen = user.LastSeen
                    },
                    Message = "Registration successful"
                };

                _logger.LogInformation("User {Username} registered successfully", request.Username);
                return Ok(response);
            }
            catch (UsernameAlreadyExistsException)
            {
                _logger.LogWarning("Registration failed: Username {Username} already exists", request.Username);
                return Conflict(new RegisterResponse
                {
                    Success = false,
                    Message = "Username already exists"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed for user {Username}", request.Username);
                return StatusCode(500, new RegisterResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost("logout")]
        public async Task<ActionResult<LogoutResponse>> Logout([FromBody] LogoutRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Logout request for user {UserId}", request.UserId);

                await _userService.LogoutAsync(request.UserId);

                var response = new LogoutResponse
                {
                    Success = true,
                    Message = "Logout successful"
                };

                _logger.LogInformation("User {UserId} logged out successfully", request.UserId);
                return Ok(response);
            }
            catch (UserNotFoundException)
            {
                _logger.LogWarning("Logout failed: User {UserId} not found", request.UserId);
                return NotFound(new LogoutResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed for user {UserId}", request.UserId);
                return StatusCode(500, new LogoutResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpGet("users/online")]
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
                    LastSeen = u.LastSeen
                }).ToList();

                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve online users");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("users/{userId}")]
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
                    LastSeen = user.LastSeen
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

        [HttpPost("users/{userId}/status")]
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
    }

    // Request/Response Models
    public class LoginRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;
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

    public class RegisterRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }
    }

    public class RegisterResponse
    {
        public bool Success { get; set; }
        public UserDto? User { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }

    public class LogoutResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class UpdateStatusRequest
    {
        [Required]
        public bool IsOnline { get; set; }
    }

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }
    }
}
