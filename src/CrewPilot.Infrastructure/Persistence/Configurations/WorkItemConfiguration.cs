using CrewPilot.Core.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

/// <summary>WorkItem 采用 TPH 继承，鉴别列 WorkItemType。</summary>
public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Title).HasMaxLength(500).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(16000);
        builder.Property(w => w.AcceptanceCriteria).HasMaxLength(8000);
        builder.Property(w => w.Priority).HasConversion<string>().HasMaxLength(8);
        builder.Property(w => w.EstimatedHours).HasPrecision(9, 2);
        builder.Property(w => w.RemainingHours).HasPrecision(9, 2);
        builder.Property(w => w.BranchUrl).HasMaxLength(500);
        builder.Property(w => w.PrUrl).HasMaxLength(500);
        builder.Ignore(w => w.LoggedHours);
        builder.HasIndex(w => w.ProjectId);
        builder.HasOne(w => w.Assignee)
            .WithMany()
            .HasForeignKey(w => w.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(w => w.Iteration)
            .WithMany(i => i.WorkItems)
            .HasForeignKey(w => w.IterationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasDiscriminator<string>("WorkItemType")
            .HasValue<Requirement>("requirement")
            .HasValue<Defect>("defect")
            .HasValue<TaskItem>("task");
    }
}

public class RequirementConfiguration : IEntityTypeConfiguration<Requirement>
{
    public void Configure(EntityTypeBuilder<Requirement> builder)
    {
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
    }
}

public class DefectConfiguration : IEntityTypeConfiguration<Defect>
{
    public void Configure(EntityTypeBuilder<Defect> builder)
    {
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.Severity).HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.ReproSteps).HasMaxLength(8000);
        builder.Property(d => d.Environment).HasMaxLength(1000);
    }
}

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.Property(t => t.ParentWorkItemId);
    }
}
