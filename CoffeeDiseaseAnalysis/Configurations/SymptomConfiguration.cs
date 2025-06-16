using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CoffeeDiseaseAnalysis.Data.Entities;

namespace CoffeeDiseaseAnalysis.Configurations
{
    public class SymptomConfiguration : IEntityTypeConfiguration<Symptom>
    {
        public void Configure(EntityTypeBuilder<Symptom> builder)
        {
            builder.HasIndex(e => e.Name).IsUnique();
            builder.HasIndex(e => e.Category);
            builder.HasIndex(e => e.IsActive);

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
            builder.Property(e => e.Description).HasMaxLength(500);
            builder.Property(e => e.Category).HasMaxLength(100).IsRequired();
            builder.Property(e => e.IsActive).HasDefaultValue(true);
            builder.Property(e => e.Weight).HasDefaultValue(1.0m).HasColumnType("decimal(3,2)");

            // Relationships
            builder.HasMany(e => e.LeafImageSymptoms)
                   .WithOne(l => l.Symptom)
                   .HasForeignKey(l => l.SymptomId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}