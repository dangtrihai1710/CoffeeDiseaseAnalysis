using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CoffeeDiseaseAnalysis.Data.Entities;

namespace CoffeeDiseaseAnalysis.Configurations
{
    public class LeafImageConfiguration : IEntityTypeConfiguration<LeafImage>
    {
        public void Configure(EntityTypeBuilder<LeafImage> builder)
        {
            builder.HasIndex(e => e.ImageHash);
            builder.HasIndex(e => e.UploadDate);
            builder.HasIndex(e => e.UserId);
            builder.HasIndex(e => e.ImageStatus);

            builder.Property(e => e.UploadDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(e => e.FilePath).HasMaxLength(500).IsRequired();
            builder.Property(e => e.ImageStatus).HasMaxLength(50).HasDefaultValue("Pending");
            builder.Property(e => e.ImageHash).HasMaxLength(32);
            builder.Property(e => e.FileExtension).HasMaxLength(10);

            // Relationships - Chỉ giữ CASCADE cho User -> LeafImage
            builder.HasOne(e => e.User)
                   .WithMany(u => u.LeafImages)
                   .HasForeignKey(e => e.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Các relationships khác sử dụng RESTRICT để tránh cascade cycles
            builder.HasMany(e => e.Predictions)
                   .WithOne(p => p.LeafImage)
                   .HasForeignKey(p => p.LeafImageId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(e => e.LeafImageSymptoms)
                   .WithOne(s => s.LeafImage)
                   .HasForeignKey(s => s.LeafImageId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(e => e.PredictionLogs)
                   .WithOne(l => l.LeafImage)
                   .HasForeignKey(l => l.LeafImageId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(e => e.TrainingDataRecords)
                   .WithOne(t => t.LeafImage)
                   .HasForeignKey(t => t.LeafImageId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}