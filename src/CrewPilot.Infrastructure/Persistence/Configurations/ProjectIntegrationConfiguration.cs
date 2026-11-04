using CrewPilot.Core.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class ProjectIntegrationConfiguration : IEntityTypeConfiguration<ProjectIntegration>
{
    public void Configure(EntityTypeBuilder<ProjectIntegration> builder)
    {
        builder.ToTable("ProjectIntegrations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Platform).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.TokenProtected).HasMaxLength(4000).IsRequired();
        builder.Property(i => i.Repository).HasMaxLength(300);
        builder.HasIndex(i => i.ProjectId).IsUnique();
        builder.HasOne(i => i.Project)
            .WithMany()
            .HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
