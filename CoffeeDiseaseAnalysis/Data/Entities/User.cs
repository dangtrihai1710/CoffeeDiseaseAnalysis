using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CoffeeDiseaseAnalysis.Data.Entities
{
    public class User : IdentityUser
    {
        [MaxLength(100)]
        public string? FullName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(20)]
        public string Role { get; set; } = "User"; // User, Expert, Admin

        // Navigation properties
        public virtual ICollection<LeafImage> LeafImages { get; set; } = new List<LeafImage>();
        public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    }
}