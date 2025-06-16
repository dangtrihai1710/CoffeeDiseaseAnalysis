using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoffeeDiseaseAnalysis.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets sẽ được thêm sau khi tạo các Entity classes
        // public DbSet<LeafImage> LeafImages { get; set; }
        // public DbSet<Prediction> Predictions { get; set; }
        // public DbSet<Feedback> Feedbacks { get; set; }
        // ... các DbSet khác

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configurations sẽ được thêm sau
        }
    }
}