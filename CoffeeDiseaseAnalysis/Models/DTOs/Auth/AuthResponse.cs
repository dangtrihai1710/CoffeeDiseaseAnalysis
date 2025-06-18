// File: CoffeeDiseaseAnalysis/Models/DTOs/Auth/AuthResponse.cs
namespace CoffeeDiseaseAnalysis.Models.DTOs.Auth
{
    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public UserDto? User { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}