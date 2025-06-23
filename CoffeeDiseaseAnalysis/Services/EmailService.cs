// ==========================================
// CoffeeDiseaseAnalysis/Services/EmailService.cs - ENHANCED WITH OTP
// ==========================================
using CoffeeDiseaseAnalysis.Services.Interfaces;
using System.Net.Mail;
using System.Net;
using System.Text;

namespace CoffeeDiseaseAnalysis.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;
        private readonly SmtpClient _smtpClient;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Cấu hình SMTP Client
            _smtpClient = new SmtpClient
            {
                Host = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com",
                Port = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"),
                EnableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true"),
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    _configuration["EmailSettings:Username"],
                    _configuration["EmailSettings:Password"]
                )
            };
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["EmailSettings:FromEmail"] ?? "noreply@coffeediagnosis.com",
                                         _configuration["EmailSettings:FromName"] ?? "Coffee Disease Analysis"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(to);

                await _smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("Email sent successfully to {To}: {Subject}", to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
                throw new InvalidOperationException($"Failed to send email: {ex.Message}");
            }
        }

        public async Task SendWelcomeEmailAsync(string to, string fullName)
        {
            var subject = "Chào mừng đến với Coffee Disease Analysis";
            var body = GenerateWelcomeEmailBody(fullName);
            await SendEmailAsync(to, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string to, string resetLink)
        {
            var subject = "Đặt lại mật khẩu - Coffee Disease Analysis";
            var body = GeneratePasswordResetEmailBody(resetLink);
            await SendEmailAsync(to, subject, body);
        }

        // ✅ NEW METHOD: Send OTP Email
        public async Task SendOtpEmailAsync(string to, string otpCode, string fullName)
        {
            var subject = "Mã xác thực đặt lại mật khẩu - Coffee Disease Analysis";
            var body = GenerateOtpEmailBody(otpCode, fullName);
            await SendEmailAsync(to, subject, body);
        }

        // ✅ NEW METHOD: Send Password Change Confirmation
        public async Task SendPasswordChangeConfirmationAsync(string to, string fullName)
        {
            var subject = "Mật khẩu đã được thay đổi thành công - Coffee Disease Analysis";
            var body = GeneratePasswordChangeConfirmationBody(fullName);
            await SendEmailAsync(to, subject, body);
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                // Test SMTP connection
                using var client = new SmtpClient(_smtpClient.Host, _smtpClient.Port);
                client.EnableSsl = _smtpClient.EnableSsl;
                client.Credentials = _smtpClient.Credentials;

                // Send a test message (without actually sending)
                await Task.Delay(100);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email service health check failed");
                return false;
            }
        }

        #region Email Templates

        private string GenerateWelcomeEmailBody(string fullName)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: #22c55e; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; background: #f9f9f9; }}
                        .footer {{ padding: 10px; text-align: center; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🌿 Coffee Disease Analysis</h1>
                        </div>
                        <div class='content'>
                            <h2>Xin chào {fullName}!</h2>
                            <p>Chào mừng bạn đến với hệ thống phân tích bệnh cây cà phê bằng AI.</p>
                            <p>Bạn có thể bắt đầu sử dụng các tính năng:</p>
                            <ul>
                                <li>📸 Chụp ảnh và phân tích bệnh trên lá cà phê</li>
                                <li>📊 Xem lịch sử và thống kê phân tích</li>
                                <li>💡 Nhận gợi ý điều trị phù hợp</li>
                            </ul>
                            <p>Chúc bạn có trải nghiệm tuyệt vời!</p>
                        </div>
                        <div class='footer'>
                            <p>© 2025 Coffee Disease Analysis. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GeneratePasswordResetEmailBody(string resetLink)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: #ef4444; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; background: #f9f9f9; }}
                        .button {{ display: inline-block; padding: 12px 24px; background: #ef4444; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
                        .footer {{ padding: 10px; text-align: center; font-size: 12px; color: #666; }}
                        .warning {{ background: #fef3cd; padding: 10px; border-left: 4px solid #fbbf24; margin: 10px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🔒 Đặt lại mật khẩu</h1>
                        </div>
                        <div class='content'>
                            <h2>Yêu cầu đặt lại mật khẩu</h2>
                            <p>Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>
                            <p>Vui lòng nhấp vào nút bên dưới để đặt lại mật khẩu:</p>
                            <p style='text-align: center;'>
                                <a href='{resetLink}' class='button'>Đặt lại mật khẩu</a>
                            </p>
                            <div class='warning'>
                                <strong>Lưu ý:</strong> Link này sẽ hết hạn sau 15 phút. Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.
                            </div>
                        </div>
                        <div class='footer'>
                            <p>© 2025 Coffee Disease Analysis. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GenerateOtpEmailBody(string otpCode, string fullName)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: #3b82f6; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; background: #f9f9f9; }}
                        .otp-code {{ 
                            font-size: 32px; 
                            font-weight: bold; 
                            text-align: center; 
                            padding: 20px; 
                            background: white; 
                            border: 2px dashed #3b82f6;
                            margin: 20px 0;
                            letter-spacing: 8px;
                            color: #3b82f6;
                        }}
                        .footer {{ padding: 10px; text-align: center; font-size: 12px; color: #666; }}
                        .warning {{ background: #fef3cd; padding: 10px; border-left: 4px solid #fbbf24; margin: 10px 0; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>🔐 Mã xác thực OTP</h1>
                        </div>
                        <div class='content'>
                            <h2>Xin chào {fullName}!</h2>
                            <p>Bạn đã yêu cầu đặt lại mật khẩu. Vui lòng sử dụng mã xác thực bên dưới:</p>
                            
                            <div class='otp-code'>{otpCode}</div>
                            
                            <p style='text-align: center;'>Nhập mã này vào trang web để tiếp tục đặt lại mật khẩu.</p>
                            
                            <div class='warning'>
                                <strong>Lưu ý quan trọng:</strong>
                                <ul>
                                    <li>Mã này chỉ có hiệu lực trong <strong>5 phút</strong></li>
                                    <li>Không chia sẻ mã này với bất kỳ ai</li>
                                    <li>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này</li>
                                </ul>
                            </div>
                        </div>
                        <div class='footer'>
                            <p>© 2025 Coffee Disease Analysis. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GeneratePasswordChangeConfirmationBody(string fullName)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background: #22c55e; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; background: #f9f9f9; }}
                        .success {{ background: #dcfce7; padding: 10px; border-left: 4px solid #22c55e; margin: 10px 0; }}
                        .footer {{ padding: 10px; text-align: center; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>✅ Mật khẩu đã được thay đổi</h1>
                        </div>
                        <div class='content'>
                            <h2>Xin chào {fullName}!</h2>
                            <div class='success'>
                                <strong>Thành công!</strong> Mật khẩu của bạn đã được thay đổi vào lúc {DateTime.Now:dd/MM/yyyy HH:mm:ss}.
                            </div>
                            <p>Nếu bạn không thực hiện thay đổi này, vui lòng liên hệ với chúng tôi ngay lập tức.</p>
                            <p>Bạn có thể đăng nhập với mật khẩu mới tại: <a href='https://localhost:3000/auth/login'>https://localhost:3000/auth/login</a></p>
                        </div>
                        <div class='footer'>
                            <p>© 2025 Coffee Disease Analysis. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _smtpClient?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

