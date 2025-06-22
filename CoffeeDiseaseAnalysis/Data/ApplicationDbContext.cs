// File: CoffeeDiseaseAnalysis/Data/ApplicationDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data.Entities;

namespace CoffeeDiseaseAnalysis.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // DbSets
        public DbSet<LeafImage> LeafImages { get; set; }
        public DbSet<Prediction> Predictions { get; set; }
        public DbSet<Symptom> Symptoms { get; set; }
        public DbSet<LeafImageSymptom> LeafImageSymptoms { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<PredictionLog> PredictionLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure composite key for LeafImageSymptom
            builder.Entity<LeafImageSymptom>()
                .HasKey(lis => new { lis.LeafImageId, lis.SymptomId });

            // Configure relationships
            builder.Entity<LeafImage>()
                .HasOne(li => li.User)
                .WithMany(u => u.LeafImages)
                .HasForeignKey(li => li.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Prediction>()
                .HasOne(p => p.LeafImage)
                .WithMany(li => li.Predictions)
                .HasForeignKey(p => p.LeafImageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Feedback>()
                .HasOne(f => f.Prediction)
                .WithMany(p => p.Feedbacks)
                .HasForeignKey(f => f.PredictionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Feedback>()
                .HasOne(f => f.User)
                .WithMany(u => u.Feedbacks)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<LeafImageSymptom>()
                .HasOne(lis => lis.LeafImage)
                .WithMany(li => li.LeafImageSymptoms)
                .HasForeignKey(lis => lis.LeafImageId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<LeafImageSymptom>()
                .HasOne(lis => lis.Symptom)
                .WithMany(s => s.LeafImageSymptoms)
                .HasForeignKey(lis => lis.SymptomId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PredictionLog>()
                .HasOne(pl => pl.LeafImage)
                .WithMany(li => li.PredictionLogs)
                .HasForeignKey(pl => pl.LeafImageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Seed data
            SeedData(builder);
        }

        private void SeedData(ModelBuilder builder)
        {
            // Seed Symptoms
            builder.Entity<Symptom>().HasData(
                new Symptom { Id = 1, Name = "Đốm nâu trên lá", Description = "Xuất hiện các đốm màu nâu tròn trên bề mặt lá", Category = "Visual" },
                new Symptom { Id = 2, Name = "Lá vàng", Description = "Lá chuyển màu vàng bất thường", Category = "Visual" },
                new Symptom { Id = 3, Name = "Đường hầm trên lá", Description = "Các đường hầm uốn khúc do sâu đục", Category = "Physical" },
                new Symptom { Id = 4, Name = "Đốm cam dưới lá", Description = "Xuất hiện đốm màu cam ở mặt dưới lá", Category = "Visual" },
                new Symptom { Id = 5, Name = "Lá khô và rụng", Description = "Lá khô héo và rụng sớm", Category = "Physical" },
                new Symptom { Id = 6, Name = "Viền vàng quanh đốm", Description = "Đốm bệnh có viền màu vàng", Category = "Visual" },
                new Symptom { Id = 7, Name = "Lá cong và biến dạng", Description = "Lá bị cong và mất hình dạng tự nhiên", Category = "Physical" }
            );
        }
    }
}