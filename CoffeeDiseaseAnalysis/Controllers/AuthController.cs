// ===================================================================
// 1. COMPLETE FIXED AuthController.cs
// ===================================================================
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Models.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CoffeeDiseaseAnalysis.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Đăng ký tài khoản mới
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("📝 Registration attempt for: {Email}", request.Email);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                    _logger.LogWarning("❌ Registration validation failed: {Errors}", string.Join(", ", errors));

                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = errors
                    });
                }

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    _logger.LogWarning("❌ Registration failed - User already exists: {Email}", request.Email);
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Email đã được sử dụng",
                        Errors = new List<string> { "Tài khoản với email này đã tồn tại" }
                    });
                }

                // Create new user
                var user = new User
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    Role = "User",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("❌ Registration failed for {Email}: {Errors}",
                        request.Email, string.Join(", ", result.Errors.Select(e => e.Description)));

                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Đăng ký thất bại",
                        Errors = result.Errors.Select(e => e.Description).ToList()
                    });
                }

                // Add user to default role
                await _userManager.AddToRoleAsync(user, "User");

                _logger.LogInformation("✅ Registration successful for: {Email}", request.Email);

                // Generate JWT token for immediate login
                var token = await GenerateJwtToken(user);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Đăng ký thành công",
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        Role = user.Role,
                        CreatedAt = user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Registration error for {Email}", request?.Email ?? "unknown");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi đăng ký",
                    Errors = new List<string> { $"Lỗi hệ thống: {ex.Message}" }
                });
            }
        }

        /// <summary>
        /// Đăng nhập - FIXED JWT GENERATION
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation("🔐 Login attempt for: {Email}", request.Email);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = errors
                    });
                }

                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    _logger.LogWarning("❌ Login failed - User not found: {Email}", request.Email);
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Email hoặc mật khẩu không đúng",
                        Errors = new List<string> { "Thông tin đăng nhập không hợp lệ" }
                    });
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("❌ Login failed - Invalid password for: {Email}", request.Email);

                    if (result.IsLockedOut)
                    {
                        return BadRequest(new AuthResponse
                        {
                            Success = false,
                            Message = "Tài khoản đã bị khóa tạm thời",
                            Errors = new List<string> { "Vui lòng thử lại sau" }
                        });
                    }

                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Email hoặc mật khẩu không đúng",
                        Errors = new List<string> { "Thông tin đăng nhập không hợp lệ" }
                    });
                }

                // ✅ GENERATE JWT TOKEN - FIXED
                var token = await GenerateJwtToken(user);

                _logger.LogInformation("✅ Login successful for: {Email}", request.Email);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Đăng nhập thành công",
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        Role = user.Role,
                        CreatedAt = user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Login error for: {Email}", request?.Email ?? "unknown");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi đăng nhập",
                    Errors = new List<string> { $"Lỗi hệ thống: {ex.Message}" }
                });
            }
        }

        /// <summary>
        /// Lấy thông tin user hiện tại - FIXED AUTHORIZATION
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<AuthResponse>> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("❌ GetCurrentUser - No user ID in token");
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Token không hợp lệ",
                        Errors = new List<string> { "Vui lòng đăng nhập lại" }
                    });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("❌ GetCurrentUser - User not found: {UserId}", userId);
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Người dùng không tồn tại",
                        Errors = new List<string> { "Tài khoản không hợp lệ" }
                    });
                }

                _logger.LogInformation("✅ GetCurrentUser successful: {Email}", user.Email);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Lấy thông tin thành công",
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        Role = user.Role,
                        CreatedAt = user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ GetCurrentUser error");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra",
                    Errors = new List<string> { $"Lỗi hệ thống: {ex.Message}" }
                });
            }
        }

        /// <summary>
        /// Đăng xuất
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult<AuthResponse>> Logout()
        {
            try
            {
                await _signInManager.SignOutAsync();
                _logger.LogInformation("✅ User logged out successfully");

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Đăng xuất thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Logout error");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi đăng xuất",
                    Errors = new List<string> { $"Lỗi hệ thống: {ex.Message}" }
                });
            }
        }

        #region Private Methods

        /// <summary>
        /// Generate JWT Token - FIXED IMPLEMENTATION
        /// </summary>
        private async Task<string> GenerateJwtToken(User user)
        {
            try
            {
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!CoffeeDiseaseAnalysis2024";
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var roles = await _userManager.GetRolesAsync(user);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.UserName ?? user.Email),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("FullName", user.FullName),
                    new Claim("UserId", user.Id),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
                };

                // Add role claims
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var token = new JwtSecurityToken(
                    issuer: jwtSettings["Issuer"] ?? "CoffeeDiseaseAnalysis",
                    audience: jwtSettings["Audience"] ?? "CoffeeDiseaseAnalysis",
                    claims: claims,
                    expires: DateTime.UtcNow.AddDays(7), // 7 days expiration
                    signingCredentials: creds
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                _logger.LogInformation("✅ JWT token generated for user: {Email}", user.Email);

                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating JWT token for user: {Email}", user.Email);
                throw new InvalidOperationException($"Could not generate JWT token: {ex.Message}", ex);
            }
        }

        #endregion
    }
}