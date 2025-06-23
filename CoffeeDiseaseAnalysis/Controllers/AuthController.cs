// ===================================================================
// CoffeeDiseaseAnalysis/Controllers/AuthController.cs - FIXED USERDTO ISSUE
// ===================================================================
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Models.DTOs.Auth;
using CoffeeDiseaseAnalysis.Services.Interfaces;
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
        private readonly IEmailService _emailService;
        private readonly IOtpService _otpService;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ILogger<AuthController> logger,
            IEmailService emailService,
            IOtpService otpService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _logger = logger;
            _emailService = emailService;
            _otpService = otpService;
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
                _logger.LogInformation("Registration attempt for email: {Email}", request.Email);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = errors.ToList()
                    });
                }

                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Email đã được sử dụng"
                    });
                }

                // Create new user
                var user = new User
                {
                    UserName = request.Email,
                    Email = request.Email,
                    FullName = request.FullName,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, request.Password);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description);
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Đăng ký thất bại",
                        Errors = errors.ToList()
                    });
                }

                // Add role
                await _userManager.AddToRoleAsync(user, "User");

                // Send welcome email
                try
                {
                    await _emailService.SendWelcomeEmailAsync(user.Email!, user.FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send welcome email to {Email}", user.Email);
                }

                _logger.LogInformation("User registered successfully: {Email}", request.Email);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Đăng ký thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error for email: {Email}", request.Email);
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Lỗi server"
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
                _logger.LogInformation("Login attempt for email: {Email}", request.Email);

                if (!ModelState.IsValid)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Email hoặc mật khẩu không hợp lệ"
                    });
                }

                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Email hoặc mật khẩu không đúng"
                    });
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
                if (!result.Succeeded)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Email hoặc mật khẩu không đúng"
                    });
                }

                var token = await GenerateJwtToken(user);
                var roles = await _userManager.GetRolesAsync(user);

                _logger.LogInformation("User logged in successfully: {Email}", request.Email);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Đăng nhập thành công",
                    Token = token,
                    User = new UserDto  // ✅ FIXED: Use UserDto instead of UserInfo
                    {
                        Id = user.Id,
                        Email = user.Email!,
                        FullName = user.FullName,
                        Role = roles.FirstOrDefault() ?? "User",
                        CreatedAt = user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for email: {Email}", request.Email);
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Lỗi server"
                });
            }
        }

        /// <summary>
        /// ✅ NEW: Quên mật khẩu - Gửi OTP qua email
        /// </summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                _logger.LogInformation("Forgot password request for email: {Email}", request.Email);

                if (!ModelState.IsValid)
                {
                    return BadRequest(new ForgotPasswordResponse
                    {
                        Success = false,
                        Message = "Email không hợp lệ"
                    });
                }

                // Kiểm tra user có tồn tại không
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    // Vì lý do bảo mật, không tiết lộ user không tồn tại
                    _logger.LogWarning("Forgot password attempt for non-existent email: {Email}", request.Email);
                    return Ok(new ForgotPasswordResponse
                    {
                        Success = true,
                        Message = "Nếu email tồn tại, mã OTP đã được gửi",
                        Email = request.Email,
                        OtpExpiresAt = DateTime.UtcNow.AddMinutes(5),
                        OtpExpiryMinutes = 5
                    });
                }

                // Tạo OTP
                var otpCode = _otpService.GenerateOtp(request.Email);

                // Gửi OTP qua email
                try
                {
                    await _emailService.SendOtpEmailAsync(request.Email, otpCode, user.FullName);
                    _logger.LogInformation("OTP sent successfully to email: {Email}", request.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send OTP email to: {Email}", request.Email);
                    return StatusCode(500, new ForgotPasswordResponse
                    {
                        Success = false,
                        Message = "Không thể gửi email. Vui lòng thử lại sau."
                    });
                }

                return Ok(new ForgotPasswordResponse
                {
                    Success = true,
                    Message = "Mã OTP đã được gửi đến email của bạn",
                    Email = request.Email,
                    OtpExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    OtpExpiryMinutes = 5
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Forgot password error for email: {Email}", request.Email);
                return StatusCode(500, new ForgotPasswordResponse
                {
                    Success = false,
                    Message = "Lỗi server"
                });
            }
        }

        /// <summary>
        /// ✅ NEW: Xác thực OTP
        /// </summary>
        [HttpPost("verify-otp")]
        [AllowAnonymous]
        public async Task<ActionResult<VerifyOtpResponse>> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                _logger.LogInformation("OTP verification request for email: {Email}", request.Email);

                if (!ModelState.IsValid)
                {
                    return BadRequest(new VerifyOtpResponse
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ"
                    });
                }

                // Kiểm tra user có tồn tại không
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return BadRequest(new VerifyOtpResponse
                    {
                        Success = false,
                        Message = "Email không tồn tại"
                    });
                }

                // Xác thực OTP
                var isValidOtp = _otpService.ValidateOtp(request.Email, request.OtpCode);

                if (!isValidOtp)
                {
                    _logger.LogWarning("Invalid OTP attempt for email: {Email}", request.Email);
                    return BadRequest(new VerifyOtpResponse
                    {
                        Success = false,
                        Message = "Mã OTP không đúng hoặc đã hết hạn",
                        IsOtpValid = false
                    });
                }

                _logger.LogInformation("OTP verified successfully for email: {Email}", request.Email);

                return Ok(new VerifyOtpResponse
                {
                    Success = true,
                    Message = "Xác thực OTP thành công",
                    Email = request.Email,
                    IsOtpValid = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP verification error for email: {Email}", request.Email);
                return StatusCode(500, new VerifyOtpResponse
                {
                    Success = false,
                    Message = "Lỗi server"
                });
            }
        }

        /// <summary>
        /// ✅ NEW: Đặt lại mật khẩu với OTP
        /// </summary>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<ActionResult<ResetPasswordResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                _logger.LogInformation("Password reset request for email: {Email}", request.Email);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                    return BadRequest(new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ"
                    });
                }

                // Kiểm tra user có tồn tại không
                var user = await _userManager.FindByEmailAsync(request.Email);
                if (user == null)
                {
                    return BadRequest(new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Email không tồn tại"
                    });
                }

                // Xác thực OTP một lần nữa
                var isValidOtp = _otpService.ValidateOtp(request.Email, request.OtpCode);
                if (!isValidOtp)
                {
                    _logger.LogWarning("Invalid OTP for password reset, email: {Email}", request.Email);
                    return BadRequest(new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Mã OTP không đúng hoặc đã hết hạn"
                    });
                }

                // Đặt lại mật khẩu
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);

                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description);
                    return BadRequest(new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Đặt lại mật khẩu thất bại"
                    });
                }

                // Vô hiệu hóa OTP đã sử dụng
                _otpService.InvalidateOtp(request.Email);

                // Gửi email xác nhận thay đổi mật khẩu
                try
                {
                    await _emailService.SendPasswordChangeConfirmationAsync(request.Email, user.FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send password change confirmation email to {Email}", request.Email);
                }

                _logger.LogInformation("Password reset successfully for email: {Email}", request.Email);

                return Ok(new ResetPasswordResponse
                {
                    Success = true,
                    Message = "Đặt lại mật khẩu thành công",
                    Email = request.Email,
                    PasswordChangedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset error for email: {Email}", request.Email);
                return StatusCode(500, new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Lỗi server"
                });
            }
        }

        /// <summary>
        /// Đổi mật khẩu (yêu cầu đăng nhập)
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult<AuthResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Người dùng không hợp lệ"
                    });
                }

                var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = result.Errors.Select(e => e.Description);
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Đổi mật khẩu thất bại",
                        Errors = errors.ToList()
                    });
                }

                _logger.LogInformation("Password changed successfully for user: {Email}", user.Email);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Đổi mật khẩu thành công"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Change password error");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Lỗi server"
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
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Người dùng không hợp lệ"
                    });
                }

                var roles = await _userManager.GetRolesAsync(user);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Thành công",
                    User = new UserDto  // ✅ FIXED: Use UserDto instead of UserInfo
                    {
                        Id = user.Id,
                        Email = user.Email!,
                        FullName = user.FullName,
                        Role = roles.FirstOrDefault() ?? "User",
                        CreatedAt = user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get current user error");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Lỗi server"
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
                _logger.LogError(ex, "Logout error");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Lỗi server"
                });
            }
        }

        #region Private Methods

        private async Task<string> GenerateJwtToken(User user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("fullName", user.FullName),
                new Claim("role", roles.FirstOrDefault() ?? "User")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"] ?? ""));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        #endregion
    }
}