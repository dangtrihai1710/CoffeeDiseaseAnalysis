
// ==========================================
// CoffeeDiseaseAnalysis/Services/Interfaces/IOtpService.cs - UPDATED INTERFACE
// ==========================================
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IOtpService
    {
        /// <summary>
        /// Tạo OTP mới cho email
        /// </summary>
        string GenerateOtp(string email);

        /// <summary>
        /// Xác thực OTP (không đánh dấu used)
        /// </summary>
        bool ValidateOtp(string email, string otp);

        /// <summary>
        /// ✅ NEW: Consume OTP (đánh dấu đã sử dụng)
        /// </summary>
        bool ConsumeOtp(string email, string otp);

        /// <summary>
        /// Vô hiệu hóa OTP
        /// </summary>
        void InvalidateOtp(string email);

        /// <summary>
        /// Kiểm tra OTP còn hiệu lực không
        /// </summary>
        bool IsOtpValid(string email);

        /// <summary>
        /// Lấy thời gian còn lại của OTP
        /// </summary>
        TimeSpan? GetOtpTimeRemaining(string email);
    }
}