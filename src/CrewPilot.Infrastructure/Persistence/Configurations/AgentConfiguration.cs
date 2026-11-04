using CrewPilot.Core.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Job).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.Model).HasMaxLength(200).IsRequired();
        builder.Property(a => a.SystemPrompt).HasMaxLength(16000).IsRequired();
        builder.Property(a => a.Tools).HasMaxLength(2000);
        builder.Property(a => a.CommandWhitelist).HasMaxLength(4000);
        builder.HasOne(a => a.Provider)
            .WithMany()
            .HasForeignKey(a => a.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(a => a.Runs)
            .WithOne(r => r.Agent)
            .HasForeignKey(r => r.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("AgentRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Trigger).HasMaxLength(32).IsRequired();
        builder.Property(r => r.WorkingDirectory).HasMaxLength(1000);
        builder.Property(r => r.Prompt).HasMaxLength(32000).IsRequired();
        builder.Property(r => r.Output).HasMaxLength(32000);
        builder.Property(r => r.Error).HasMaxLength(8000);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(r => r.AgentId);
        builder.HasIndex(r => r.WorkItemId);
        builder.HasOne(r => r.WorkItem)
            .WithMany()
            .HasForeignKey(r => r.WorkItemId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(r => r.Logs)
            .WithOne(l => l.AgentRun)
            .HasForeignKey(l => l.AgentRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.WorkflowRun)
            .WithMany()
            .HasForeignKey(r => r.WorkflowRunId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(r => r.WorkflowStep)
            .WithMany()
            .HasForeignKey(r => r.WorkflowStepId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class RunLogConfiguration : IEntityTypeConfiguration<RunLog>
{
    public void Configure(EntityTypeBuilder<RunLog> builder)
    {
        builder.ToTable("RunLogs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Kind).HasMaxLength(16).IsRequired();
        builder.Property(l => l.Content).HasMaxLength(32000).IsRequired();
        builder.HasIndex(l => new { l.AgentRunId, l.Sequence });
    }
}
