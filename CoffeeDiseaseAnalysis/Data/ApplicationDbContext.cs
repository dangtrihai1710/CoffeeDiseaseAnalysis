using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data.Entities;

namespace CoffeeDiseaseAnalysis.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // ✅ DbSets - Đã thêm ModelVersions và TrainingData
        public DbSet<LeafImage> LeafImages { get; set; }
        public DbSet<Prediction> Predictions { get; set; }
        public DbSet<Symptom> Symptoms { get; set; }
        public DbSet<LeafImageSymptom> LeafImageSymptoms { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<PredictionLog> PredictionLogs { get; set; }
        public DbSet<ModelVersion> ModelVersions { get; set; } // ✅ THÊM
        public DbSet<TrainingData> TrainingData { get; set; } // ✅ THÊM (TrainingDataRecords -> TrainingData)

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ✅ Configure composite key for LeafImageSymptom - FIX Lỗi Weight
            builder.Entity<LeafImageSymptom>()
                .HasKey(lis => new { lis.LeafImageId, lis.SymptomId });

            // ✅ Configure relationships
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

            // ✅ FIX TrainingData relationships
            builder.Entity<TrainingData>()
                .HasOne(td => td.LeafImage)
                .WithMany(li => li.TrainingDataRecords)
                .HasForeignKey(td => td.LeafImageId)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ ModelVersion relationships
            builder.Entity<ModelVersion>()
                .HasOne(mv => mv.CreatedByUser)
                .WithMany()
                .HasForeignKey(mv => mv.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}