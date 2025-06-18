// File: CoffeeDiseaseAnalysis/Models/DTOs/Auth/UserDto.cs
namespace CoffeeDiseaseAnalysis.Models.DTOs.Auth
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}