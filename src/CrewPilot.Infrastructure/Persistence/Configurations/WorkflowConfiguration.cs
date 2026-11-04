using CrewPilot.Core.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("Workflows");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Name).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(2000);
        builder.HasIndex(w => w.ProjectId);
        builder.HasOne(w => w.Project)
            .WithMany()
            .HasForeignKey(w => w.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(w => w.Steps)
            .WithOne(s => s.Workflow)
            .HasForeignKey(s => s.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.ToTable("WorkflowSteps");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Instruction).HasMaxLength(8000);
        builder.Property(s => s.InputContract).HasMaxLength(2000);
        builder.Property(s => s.OutputContract).HasMaxLength(2000);
        builder.Property(s => s.ApprovalPolicy).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.RequiredJob).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(s => new { s.WorkflowId, s.Order });
        builder.HasOne(s => s.Agent)
            .WithMany()
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class WorkflowRunConfiguration : IEntityTypeConfiguration<WorkflowRun>
{
    public void Configure(EntityTypeBuilder<WorkflowRun> builder)
    {
        builder.ToTable("WorkflowRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(r => r.WorkItemId);
        builder.HasIndex(r => r.WorkflowId);
        builder.HasOne(r => r.Workflow)
            .WithMany()
            .HasForeignKey(r => r.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.WorkItem)
            .WithMany()
            .HasForeignKey(r => r.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BlockerConfiguration : IEntityTypeConfiguration<Blocker>
{
    public void Configure(EntityTypeBuilder<Blocker> builder)
    {
        builder.ToTable("Blockers");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.Resolution).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(b => b.ContextSnapshot).HasMaxLength(16000);
        builder.Property(b => b.ResolutionNote).HasMaxLength(4000);
        builder.HasIndex(b => new { b.ProjectId, b.Status });
        builder.HasOne(b => b.WorkItem)
            .WithMany()
            .HasForeignKey(b => b.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(b => b.WorkflowRun)
            .WithMany()
            .HasForeignKey(b => b.WorkflowRunId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(b => b.AgentRun)
            .WithMany()
            .HasForeignKey(b => b.AgentRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
