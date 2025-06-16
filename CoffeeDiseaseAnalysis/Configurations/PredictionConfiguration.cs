using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CoffeeDiseaseAnalysis.Data.Entities;

namespace CoffeeDiseaseAnalysis.Configurations
{
    public class PredictionConfiguration : IEntityTypeConfiguration<Prediction>
    {
        public void Configure(EntityTypeBuilder<Prediction> builder)
        {
            builder.HasIndex(e => e.PredictionDate);
            builder.HasIndex(e => e.DiseaseName);
            builder.HasIndex(e => e.ModelVersion);
            builder.HasIndex(e => new { e.LeafImageId, e.ModelVersion });

            builder.Property(e => e.PredictionDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(e => e.DiseaseName).HasMaxLength(100).IsRequired();
            builder.Property(e => e.ModelVersion).HasMaxLength(50).IsRequired();
            builder.Property(e => e.TreatmentSuggestion).HasMaxLength(1000);
            builder.Property(e => e.SeverityLevel).HasMaxLength(50).HasDefaultValue("Unknown");

            // Decimal precision
            builder.Property(e => e.Confidence)
                   .HasColumnType("decimal(5,4)")
                   .IsRequired();

            builder.Property(e => e.FinalConfidence)
                   .HasColumnType("decimal(5,4)");

            // Relationships
            builder.HasOne(e => e.LeafImage)
                   .WithMany(l => l.Predictions)
                   .HasForeignKey(e => e.LeafImageId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Feedbacks)
                   .WithOne(f => f.Prediction)
                   .HasForeignKey(f => f.PredictionId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}