// File: CoffeeDiseaseAnalysis/Controllers/AuthController.cs
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Models.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
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
                if (!ModelState.IsValid)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                // Kiểm tra email đã tồn tại
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Email đã được sử dụng",
                        Errors = new List<string> { "Email này đã được đăng ký" }
                    });
                }

                // Tạo user mới
                var user = new User
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    Role = "User", // Mặc định là User
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, request.Password);

                if (!result.Succeeded)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Không thể tạo tài khoản",
                        Errors = result.Errors.Select(e => e.Description).ToList()
                    });
                }

                // Thêm role User cho người dùng mới
                await _userManager.AddToRoleAsync(user, "User");

                _logger.LogInformation("Người dùng mới đăng ký: {Email}", user.Email);

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
                _logger.LogError(ex, "Lỗi khi đăng ký tài khoản: {Email}", request?.Email);
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi đăng ký",
                    Errors = new List<string> { "Lỗi hệ thống" }
                });
            }
        }

        /// <summary>
        /// Đăng nhập
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                // Tìm user theo email
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Email hoặc mật khẩu không đúng",
                        Errors = new List<string> { "Thông tin đăng nhập không chính xác" }
                    });
                }

                // Kiểm tra mật khẩu
                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

                if (result.IsLockedOut)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Tài khoản đã bị khóa do đăng nhập sai quá nhiều lần",
                        Errors = new List<string> { "Vui lòng thử lại sau 15 phút" }
                    });
                }

                if (!result.Succeeded)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Email hoặc mật khẩu không đúng",
                        Errors = new List<string> { "Thông tin đăng nhập không chính xác" }
                    });
                }

                // Tạo JWT token
                var token = await GenerateJwtToken(user);

                _logger.LogInformation("Người dùng đăng nhập thành công: {Email}", user.Email);

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
                _logger.LogError(ex, "Lỗi khi đăng nhập: {Email}", request?.Email);
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi đăng nhập",
                    Errors = new List<string> { "Lỗi hệ thống" }
                });
            }
        }

        /// <summary>
        /// Lấy thông tin user hiện tại
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
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Người dùng không tồn tại",
                        Errors = new List<string> { "Tài khoản không hợp lệ" }
                    });
                }

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
                _logger.LogError(ex, "Lỗi khi lấy thông tin user");
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
                await _signInManager.SignOutAsync();

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Đăng xuất thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng xuất");
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
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList()
                    });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userManager.FindByIdAsync(userId);

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
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Không thể đổi mật khẩu",
                        Errors = result.Errors.Select(e => e.Description).ToList()
                    });
                }

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Đổi mật khẩu thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đổi mật khẩu");
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
            string? search = null)
        {
            try
            {
                var query = _userManager.Users.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(u => u.Email.Contains(search) || u.FullName.Contains(search));
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
                    Success = true,
                    Data = users,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách users");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Có lỗi xảy ra",
                    Errors = new List<string> { "Lỗi hệ thống" }
                });
            }
        }

        #region Private Methods

        private async Task<string> GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? "CoffeeDiseaseAnalysisSecretKey2024!");

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName ?? ""),
                new Claim("role", user.Role ?? "User")
            };

            // Thêm roles vào claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7), // Token có hiệu lực 7 ngày
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = jwtSettings["Issuer"] ?? "CoffeeDiseaseAnalysis",
                Audience = jwtSettings["Audience"] ?? "CoffeeDiseaseAnalysisUsers"
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        #endregion
    }
}