// File: CoffeeDiseaseAnalysis/Data/ApplicationDbContext.cs - FIXED VERSION
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data.Entities;

namespace CoffeeDiseaseAnalysis.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<LeafImage> LeafImages { get; set; }
        public DbSet<Prediction> Predictions { get; set; }
        public DbSet<Symptom> Symptoms { get; set; }
        public DbSet<LeafImageSymptom> LeafImageSymptoms { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<PredictionLog> PredictionLogs { get; set; }
        public DbSet<ModelVersion> ModelVersions { get; set; }
        public DbSet<TrainingData> TrainingData { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ✅ FIXED: Đồng nhất với ModelSnapshot - dùng Restrict để tránh cascade cycles

            // Composite key
            builder.Entity<LeafImageSymptom>()
                .HasKey(lis => new { lis.LeafImageId, lis.SymptomId });

            // User -> LeafImage: CASCADE (User bị xóa thì xóa tất cả ảnh)
            builder.Entity<LeafImage>()
                .HasOne(li => li.User)
                .WithMany(u => u.LeafImages)
                .HasForeignKey(li => li.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // LeafImage -> Prediction: RESTRICT (Không cho xóa LeafImage nếu có Prediction)
            builder.Entity<Prediction>()
                .HasOne(p => p.LeafImage)
                .WithMany(li => li.Predictions)
                .HasForeignKey(p => p.LeafImageId)
                .OnDelete(DeleteBehavior.Restrict); // ✅ FIXED

            // Prediction -> Feedback: RESTRICT
            builder.Entity<Feedback>()
                .HasOne(f => f.Prediction)
                .WithMany(p => p.Feedbacks)
                .HasForeignKey(f => f.PredictionId)
                .OnDelete(DeleteBehavior.Restrict); // ✅ FIXED

            // User -> Feedback: NO ACTION (Tránh multiple cascade paths)
            builder.Entity<Feedback>()
                .HasOne(f => f.User)
                .WithMany(u => u.Feedbacks)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // LeafImage -> LeafImageSymptom: RESTRICT
            builder.Entity<LeafImageSymptom>()
                .HasOne(lis => lis.LeafImage)
                .WithMany(li => li.LeafImageSymptoms)
                .HasForeignKey(lis => lis.LeafImageId)
                .OnDelete(DeleteBehavior.Restrict); // ✅ FIXED

            // Symptom -> LeafImageSymptom: RESTRICT
            builder.Entity<LeafImageSymptom>()
                .HasOne(lis => lis.Symptom)
                .WithMany(s => s.LeafImageSymptoms)
                .HasForeignKey(lis => lis.SymptomId)
                .OnDelete(DeleteBehavior.Restrict); // ✅ FIXED

            // User -> LeafImageSymptom (ObservedByUser): SET NULL
            builder.Entity<LeafImageSymptom>()
                .HasOne(lis => lis.ObservedByUser)
                .WithMany()
                .HasForeignKey(lis => lis.ObservedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // LeafImage -> PredictionLog: RESTRICT
            builder.Entity<PredictionLog>()
                .HasOne(pl => pl.LeafImage)
                .WithMany(li => li.PredictionLogs)
                .HasForeignKey(pl => pl.LeafImageId)
                .OnDelete(DeleteBehavior.Restrict); // ✅ FIXED

            // LeafImage -> TrainingData: RESTRICT
            builder.Entity<TrainingData>()
                .HasOne(td => td.LeafImage)
                .WithMany(li => li.TrainingDataRecords)
                .HasForeignKey(td => td.LeafImageId)
                .OnDelete(DeleteBehavior.Restrict); // ✅ FIXED

            // User -> TrainingData (ValidatedByUser): SET NULL
            builder.Entity<TrainingData>()
                .HasOne(td => td.ValidatedByUser)
                .WithMany()
                .HasForeignKey(td => td.ValidatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Feedback -> TrainingData: SET NULL
            builder.Entity<TrainingData>()
                .HasOne(td => td.SourceFeedback)
                .WithMany()
                .HasForeignKey(td => td.FeedbackId)
                .OnDelete(DeleteBehavior.SetNull);

            // User -> ModelVersion: SET NULL
            builder.Entity<ModelVersion>()
                .HasOne(mv => mv.CreatedByUser)
                .WithMany()
                .HasForeignKey(mv => mv.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ✅ Configure decimal precision
            builder.Entity<Prediction>()
                .Property(p => p.Confidence)
                .HasPrecision(5, 4);

            builder.Entity<Prediction>()
                .Property(p => p.FinalConfidence)
                .HasPrecision(5, 4);

            builder.Entity<ModelVersion>()
                .Property(mv => mv.Accuracy)
                .HasPrecision(5, 4);

            builder.Entity<ModelVersion>()
                .Property(mv => mv.ValidationAccuracy)
                .HasPrecision(5, 4);

            builder.Entity<ModelVersion>()
                .Property(mv => mv.TestAccuracy)
                .HasPrecision(5, 4);

            builder.Entity<Symptom>()
                .Property(s => s.Weight)
                .HasPrecision(3, 2);

            // ✅ Default values
            builder.Entity<LeafImage>()
                .Property(li => li.UploadDate)
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Entity<Prediction>()
                .Property(p => p.PredictionDate)
                .HasDefaultValueSql("GETUTCDATE()");
        }
    }
}