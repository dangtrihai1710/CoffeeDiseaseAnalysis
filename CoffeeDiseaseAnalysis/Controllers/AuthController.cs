// File: CoffeeDiseaseAnalysis/Controllers/AuthController.cs - FIXED JWT & ERROR HANDLING
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Models.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

                // Kiểm tra email đã tồn tại
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    _logger.LogWarning("❌ Registration failed - Email already exists: {Email}", request.Email);
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Email đã được sử dụng",
                        Errors = new List<string> { "Vui lòng sử dụng email khác" }
                    });
                }

                // Tạo user mới
                var user = new User
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    Role = request.Role ?? "User",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, request.Password);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    _logger.LogError("❌ User creation failed: {Errors}", string.Join(", ", errors));

                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Không thể tạo tài khoản",
                        Errors = errors
                    });
                }

                // Thêm role cho user
                if (!string.IsNullOrEmpty(user.Role))
                {
                    var roleExists = await _roleManager.RoleExistsAsync(user.Role);
                    if (!roleExists)
                    {
                        await _roleManager.CreateAsync(new IdentityRole(user.Role));
                    }
                    await _userManager.AddToRoleAsync(user, user.Role);
                }

                _logger.LogInformation("✅ Registration successful for: {Email}", request.Email);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Đăng ký thành công",
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
                _logger.LogError(ex, "❌ Registration error for: {Email}", request?.Email);
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi đăng ký",
                    Errors = new List<string> { "Lỗi hệ thống" }
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
                _logger.LogError(ex, "❌ Login error for: {Email}", request?.Email);
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi đăng nhập",
                    Errors = new List<string> { "Lỗi hệ thống" }
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
                    Errors = new List<string> { "Lỗi hệ thống" }
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
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                _logger.LogInformation("🔓 Logout for user: {UserId}", userId);

                await _signInManager.SignOutAsync();

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
                    Errors = new List<string> { "Lỗi hệ thống" }
                });
            }
        }

        /// <summary>
        /// Đổi mật khẩu
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult<AuthResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
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

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userManager.FindByIdAsync(userId!);

                if (user == null)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Người dùng không tồn tại"
                    });
                }

                var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Không thể đổi mật khẩu",
                        Errors = errors
                    });
                }

                _logger.LogInformation("✅ Password changed for user: {Email}", user.Email);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Đổi mật khẩu thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Change password error");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi đổi mật khẩu",
                    Errors = new List<string> { "Lỗi hệ thống" }
                });
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả users (chỉ Admin)
        /// </summary>
        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<object>> GetAllUsers(
            int pageNumber = 1,
            int pageSize = 10,
            string? searchTerm = null)
        {
            try
            {
                var query = _userManager.Users.AsQueryable();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(u => u.Email.Contains(searchTerm) ||
                                           u.FullName.Contains(searchTerm));
                }

                var totalCount = await query.CountAsync();

                var users = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        Email = u.Email,
                        FullName = u.FullName,
                        Role = u.Role,
                        CreatedAt = u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    Data = users,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ GetAllUsers error");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi lấy danh sách người dùng",
                    Errors = new List<string> { "Lỗi hệ thống" }
                });
            }
        }

        // ✅ PRIVATE HELPER METHOD - GENERATE JWT TOKEN
        private async Task<string> GenerateJwtToken(User user)
        {
            try
            {
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
                var issuer = jwtSettings["Issuer"] ?? "CoffeeDiseaseAnalysis";
                var audience = jwtSettings["Audience"] ?? "CoffeeDiseaseAnalysis";
                var expirationMinutes = int.Parse(jwtSettings["ExpirationInMinutes"] ?? "1440");

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                    new Claim("role", user.Role ?? "User"),
                    new Claim("fullName", user.FullName ?? ""),
                    new Claim("jti", Guid.NewGuid().ToString())
                };

                // Add role claims
                var userRoles = await _userManager.GetRolesAsync(user);
                foreach (var role in userRoles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var token = new JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                    signingCredentials: creds
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                _logger.LogInformation("✅ JWT token generated for user: {Email}, expires: {Expires}",
                    user.Email, token.ValidTo);

                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error generating JWT token for user: {Email}", user.Email);
                throw new InvalidOperationException("Could not generate JWT token", ex);
            }
        }
    }
}