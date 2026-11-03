using CrewPilot.Core.Prototypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class PrototypeConfiguration : IEntityTypeConfiguration<Prototype>
{
    public void Configure(EntityTypeBuilder<Prototype> builder)
    {
        builder.ToTable("Prototypes");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Content).HasMaxLength(1000000).IsRequired();
        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.CreatedByKind).HasConversion<string>().HasMaxLength(16);
        builder.Property(p => p.CreatedByName).HasMaxLength(100);
        builder.Property(p => p.ThumbnailPath).HasMaxLength(1000);
        builder.HasIndex(p => p.ProjectId);
        builder.HasOne(p => p.Requirement)
            .WithMany(r => r.Prototypes)
            .HasForeignKey(p => p.RequirementId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
