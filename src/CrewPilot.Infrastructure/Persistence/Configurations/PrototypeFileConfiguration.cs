using CrewPilot.Core.Prototypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class PrototypeFileConfiguration : IEntityTypeConfiguration<PrototypeFile>
{
    public void Configure(EntityTypeBuilder<PrototypeFile> builder)
    {
        builder.ToTable("PrototypeFiles");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Path).HasMaxLength(300).IsRequired();
        builder.Property(f => f.Content).HasMaxLength(300000).IsRequired();
        builder.HasOne(f => f.Prototype)
            .WithMany()
            .HasForeignKey(f => f.PrototypeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(f => new { f.PrototypeId, f.Path }).IsUnique();
    }
}
