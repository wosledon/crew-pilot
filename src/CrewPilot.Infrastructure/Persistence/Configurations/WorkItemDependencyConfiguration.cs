using CrewPilot.Core.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class WorkItemDependencyConfiguration : IEntityTypeConfiguration<WorkItemDependency>
{
    public void Configure(EntityTypeBuilder<WorkItemDependency> builder)
    {
        builder.ToTable("WorkItemDependencies");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(d => d.FromWorkItem)
            .WithMany()
            .HasForeignKey(d => d.FromWorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(d => d.ToWorkItem)
            .WithMany()
            .HasForeignKey(d => d.ToWorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.ProjectId);
        builder.HasIndex(d => new { d.FromWorkItemId, d.ToWorkItemId }).IsUnique();
    }
}
