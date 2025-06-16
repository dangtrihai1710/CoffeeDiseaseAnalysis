using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CoffeeDiseaseAnalysis.Data.Entities;

namespace CoffeeDiseaseAnalysis.Configurations
{
    public class ModelVersionConfiguration : IEntityTypeConfiguration<ModelVersion>
    {
        public void Configure(EntityTypeBuilder<ModelVersion> builder)
        {
            builder.HasIndex(e => new { e.ModelName, e.Version }).IsUnique();
            builder.HasIndex(e => e.IsActive);
            builder.HasIndex(e => e.IsProduction);
            builder.HasIndex(e => e.CreatedAt);
            builder.HasIndex(e => e.ModelType);

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(e => e.ModelName).HasMaxLength(100).IsRequired();
            builder.Property(e => e.Version).HasMaxLength(20).IsRequired();
            builder.Property(e => e.FilePath).HasMaxLength(500).IsRequired();
            builder.Property(e => e.Notes).HasMaxLength(1000);
            builder.Property(e => e.IsActive).HasDefaultValue(false);
            builder.Property(e => e.IsProduction).HasDefaultValue(false);
            builder.Property(e => e.TrainingDatasetVersion).HasMaxLength(100).IsRequired();
            builder.Property(e => e.ModelType).HasMaxLength(50).HasDefaultValue("CNN");
            builder.Property(e => e.FileChecksum).HasMaxLength(32);

            // Decimal precision
            builder.Property(e => e.Accuracy)
                   .HasColumnType("decimal(5,4)")
                   .IsRequired();

            builder.Property(e => e.ValidationAccuracy)
                   .HasColumnType("decimal(5,4)");

            builder.Property(e => e.TestAccuracy)
                   .HasColumnType("decimal(5,4)");

            // Relationships
            builder.HasOne(e => e.CreatedByUser)
                   .WithMany()
                   .HasForeignKey(e => e.CreatedByUserId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }
}