using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Configurations;
using System.Reflection;

namespace CoffeeDiseaseAnalysis.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets for all entities
        public DbSet<LeafImage> LeafImages { get; set; }
        public DbSet<Prediction> Predictions { get; set; }
        public DbSet<Symptom> Symptoms { get; set; }
        public DbSet<LeafImageSymptom> LeafImageSymptoms { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<PredictionLog> PredictionLogs { get; set; }
        public DbSet<ModelVersion> ModelVersions { get; set; }
        public DbSet<TrainingData> TrainingDataRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Apply all configurations automatically
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Additional configurations for entities that don't have separate configuration classes
            ConfigureLeafImageSymptom(builder);
            ConfigureFeedback(builder);
            ConfigurePredictionLog(builder);
            ConfigureTrainingData(builder);

            // Seed data
            SeedData(builder);
        }

        private void ConfigureLeafImageSymptom(ModelBuilder builder)
        {
            builder.Entity<LeafImageSymptom>(entity =>
            {
                entity.HasIndex(e => new { e.LeafImageId, e.SymptomId }).IsUnique();
                entity.HasIndex(e => e.ObservedDate);
                entity.HasIndex(e => e.Intensity);

                entity.Property(e => e.ObservedDate).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Notes).HasMaxLength(200);
                entity.Property(e => e.Intensity).HasDefaultValue(1);

                entity.HasOne(e => e.LeafImage)
                      .WithMany(l => l.LeafImageSymptoms)
                      .HasForeignKey(e => e.LeafImageId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Symptom)
                      .WithMany(s => s.LeafImageSymptoms)
                      .HasForeignKey(e => e.SymptomId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ObservedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.ObservedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }

        private void ConfigureFeedback(ModelBuilder builder)
        {
            builder.Entity<Feedback>(entity =>
            {
                entity.HasIndex(e => e.Rating);
                entity.HasIndex(e => e.FeedbackDate);
                entity.HasIndex(e => e.IsUsedForTraining);
                entity.HasIndex(e => e.FeedbackType);

                entity.Property(e => e.FeedbackDate).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.FeedbackText).HasMaxLength(1000);
                entity.Property(e => e.CorrectDiseaseName).HasMaxLength(100);
                entity.Property(e => e.IsUsedForTraining).HasDefaultValue(false);
                entity.Property(e => e.FeedbackType).HasMaxLength(50).HasDefaultValue("Manual");

                entity.HasOne(e => e.Prediction)
                      .WithMany(p => p.Feedbacks)
                      .HasForeignKey(e => e.PredictionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                      .WithMany(u => u.Feedbacks)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.NoAction);
            });
        }

        private void ConfigurePredictionLog(ModelBuilder builder)
        {
            builder.Entity<PredictionLog>(entity =>
            {
                entity.HasIndex(e => e.RequestTime);
                entity.HasIndex(e => e.ApiStatus);
                entity.HasIndex(e => e.ModelVersion);
                entity.HasIndex(e => e.RequestId);
                entity.HasIndex(e => e.ModelType);

                entity.Property(e => e.ModelType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.ApiStatus).HasMaxLength(50).IsRequired();
                entity.Property(e => e.ErrorMessage).HasMaxLength(500);
                entity.Property(e => e.ModelVersion).HasMaxLength(50).IsRequired();
                entity.Property(e => e.RequestId).HasMaxLength(100);
                entity.Property(e => e.ServerNode).HasMaxLength(50);

                entity.HasOne(e => e.LeafImage)
                      .WithMany(l => l.PredictionLogs)
                      .HasForeignKey(e => e.LeafImageId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private void ConfigureTrainingData(ModelBuilder builder)
        {
            builder.Entity<TrainingData>(entity =>
            {
                entity.HasIndex(e => e.Label);
                entity.HasIndex(e => e.Source);
                entity.HasIndex(e => e.DatasetSplit);
                entity.HasIndex(e => e.IsValidated);
                entity.HasIndex(e => e.IsUsedForTraining);
                entity.HasIndex(e => e.Quality);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Label).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Source).HasMaxLength(50).IsRequired();
                entity.Property(e => e.IsValidated).HasDefaultValue(false);
                entity.Property(e => e.DatasetSplit).HasMaxLength(50).HasDefaultValue("train");
                entity.Property(e => e.IsUsedForTraining).HasDefaultValue(false);
                entity.Property(e => e.OriginalPrediction).HasMaxLength(100);
                entity.Property(e => e.Notes).HasMaxLength(200);
                entity.Property(e => e.Quality).HasMaxLength(50).HasDefaultValue("Unknown");

                entity.Property(e => e.OriginalConfidence)
                      .HasColumnType("decimal(5,4)");

                entity.HasOne(e => e.LeafImage)
                      .WithMany(l => l.TrainingDataRecords)
                      .HasForeignKey(e => e.LeafImageId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ValidatedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.ValidatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.SourceFeedback)
                      .WithMany()
                      .HasForeignKey(e => e.FeedbackId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }

        private void SeedData(ModelBuilder builder)
        {
            // Seed Symptoms
            builder.Entity<Symptom>().HasData(
                new Symptom { Id = 1, Name = "Vệt nâu trên lá", Description = "Các vệt màu nâu xuất hiện trên bề mặt lá", Category = "Leaf", Weight = 0.8m, CreatedAt = DateTime.UtcNow },
                new Symptom { Id = 2, Name = "Vết đốm cam đỏ", Description = "Các đốm màu cam đỏ đặc trưng của bệnh rỉ sắt", Category = "Leaf", Weight = 0.9m, CreatedAt = DateTime.UtcNow },
                new Symptom { Id = 3, Name = "Lá héo", Description = "Lá bị héo, mất độ tươi", Category = "Leaf", Weight = 0.7m, CreatedAt = DateTime.UtcNow },
                new Symptom { Id = 4, Name = "Lá vàng", Description = "Lá chuyển màu vàng bất thường", Category = "Leaf", Weight = 0.6m, CreatedAt = DateTime.UtcNow },
                new Symptom { Id = 5, Name = "Đường viền lá nâu", Description = "Viền lá chuyển màu nâu", Category = "Leaf", Weight = 0.7m, CreatedAt = DateTime.UtcNow },
                new Symptom { Id = 6, Name = "Lỗ thủng trên lá", Description = "Các lỗ nhỏ do sâu đục", Category = "Leaf", Weight = 0.8m, CreatedAt = DateTime.UtcNow },
                new Symptom { Id = 7, Name = "Bề mặt lá khô", Description = "Bề mặt lá bị khô, nứt nẻ", Category = "Leaf", Weight = 0.6m, CreatedAt = DateTime.UtcNow },
                new Symptom { Id = 8, Name = "Vệt trắng", Description = "Các vệt màu trắng do nấm", Category = "Leaf", Weight = 0.75m, CreatedAt = DateTime.UtcNow },
                new Symptom { Id = 9, Name = "Lá cong vặn", Description = "Lá bị cong vặn do sâu bệnh", Category = "Leaf", Weight = 0.85m, CreatedAt = DateTime.UtcNow },
                new Symptom { Id = 10, Name = "Mép lá khô", Description = "Mép lá bị khô, cháy", Category = "Leaf", Weight = 0.65m, CreatedAt = DateTime.UtcNow }
            );

            // Seed ModelVersions
            builder.Entity<ModelVersion>().HasData(
                new ModelVersion
                {
                    Id = 1,
                    ModelName = "coffee_resnet50",
                    Version = "v1.0",
                    FilePath = "/models/coffee_resnet50_v1.0.h5",
                    Accuracy = 0.8500m,
                    ValidationAccuracy = 0.8200m,
                    TestAccuracy = 0.8100m,
                    Notes = "Mô hình ResNet50 ban đầu - baseline model",
                    IsActive = false,
                    IsProduction = false,
                    TrainingDatasetVersion = "v1.0",
                    TrainingSamples = 2000,
                    ValidationSamples = 400,
                    TestSamples = 400,
                    ModelType = "CNN",
                    FileSizeBytes = 265281000,
                    CreatedAt = DateTime.UtcNow.AddMonths(-6)
                },
                new ModelVersion
                {
                    Id = 2,
                    ModelName = "coffee_resnet50",
                    Version = "v1.1",
                    FilePath = "/models/coffee_resnet50_v1.1.onnx",
                    Accuracy = 0.8750m,
                    ValidationAccuracy = 0.8500m,
                    TestAccuracy = 0.8400m,
                    Notes = "Cải tiến với data augmentation, fine-tuning và chuyển đổi sang ONNX",
                    IsActive = true,
                    IsProduction = true,
                    TrainingDatasetVersion = "v1.1",
                    TrainingSamples = 2500,
                    ValidationSamples = 500,
                    TestSamples = 500,
                    ModelType = "CNN",
                    FileSizeBytes = 120000000,
                    CreatedAt = DateTime.UtcNow.AddMonths(-3),
                    DeployedAt = DateTime.UtcNow.AddMonths(-3).AddDays(2)
                },
                new ModelVersion
                {
                    Id = 3,
                    ModelName = "coffee_mlp",
                    Version = "v1.0",
                    FilePath = "/models/coffee_mlp_v1.0.onnx",
                    Accuracy = 0.7200m,
                    ValidationAccuracy = 0.7000m,
                    TestAccuracy = 0.6900m,
                    Notes = "MLP cho phân tích triệu chứng - hỗ trợ CNN",
                    IsActive = true,
                    IsProduction = false,
                    TrainingDatasetVersion = "v1.0",
                    TrainingSamples = 1500,
                    ValidationSamples = 300,
                    TestSamples = 300,
                    ModelType = "MLP",
                    FileSizeBytes = 5000000,
                    CreatedAt = DateTime.UtcNow.AddMonths(-2)
                },
                new ModelVersion
                {
                    Id = 4,
                    ModelName = "coffee_combined",
                    Version = "v1.0",
                    FilePath = "/models/coffee_combined_v1.0.onnx",
                    Accuracy = 0.9100m,
                    ValidationAccuracy = 0.8900m,
                    TestAccuracy = 0.8800m,
                    Notes = "Kết hợp CNN và MLP với trọng số 0.7:0.3",
                    IsActive = true,
                    IsProduction = false,
                    TrainingDatasetVersion = "v1.1",
                    TrainingSamples = 2500,
                    ValidationSamples = 500,
                    TestSamples = 500,
                    ModelType = "Combined",
                    FileSizeBytes = 125000000,
                    CreatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new ModelVersion
                {
                    Id = 5,
                    ModelName = "coffee_resnet50",
                    Version = "v2.0",
                    FilePath = "/models/coffee_resnet50_v2.0.onnx",
                    Accuracy = 0.9200m,
                    ValidationAccuracy = 0.9000m,
                    TestAccuracy = 0.8950m,
                    Notes = "Huấn luyện lại với feedback từ người dùng và SMOTE để xử lý dữ liệu không cân bằng",
                    IsActive = false,
                    IsProduction = false,
                    TrainingDatasetVersion = "v2.0",
                    TrainingSamples = 3000,
                    ValidationSamples = 600,
                    TestSamples = 600,
                    ModelType = "CNN",
                    FileSizeBytes = 118000000,
                    CreatedAt = DateTime.UtcNow.AddDays(-15)
                }
            );
        }

        // Override SaveChanges để tự động cập nhật timestamps
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Entity is LeafImage leafImage && entry.State == EntityState.Added)
                {
                    leafImage.UploadDate = DateTime.UtcNow;
                }

                if (entry.Entity is Prediction prediction && entry.State == EntityState.Added)
                {
                    prediction.PredictionDate = DateTime.UtcNow;
                }

                if (entry.Entity is Feedback feedback && entry.State == EntityState.Added)
                {
                    feedback.FeedbackDate = DateTime.UtcNow;
                }

                if (entry.Entity is LeafImageSymptom symptom && entry.State == EntityState.Added)
                {
                    symptom.ObservedDate = DateTime.UtcNow;
                }

                if (entry.Entity is TrainingData trainingData && entry.State == EntityState.Added)
                {
                    trainingData.CreatedAt = DateTime.UtcNow;
                }

                if (entry.Entity is ModelVersion modelVersion && entry.State == EntityState.Added)
                {
                    modelVersion.CreatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}