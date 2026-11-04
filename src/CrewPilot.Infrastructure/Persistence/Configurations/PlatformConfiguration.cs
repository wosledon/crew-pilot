using CrewPilot.Core.Audit;
using CrewPilot.Core.Mcp;
using CrewPilot.Core.Skills;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.PromptContent).HasMaxLength(16000).IsRequired();
        builder.Property(s => s.Tools).HasMaxLength(2000);
        builder.Property(s => s.ScriptPath).HasMaxLength(500);
    }
}

public class AgentSkillConfiguration : IEntityTypeConfiguration<AgentSkill>
{
    public void Configure(EntityTypeBuilder<AgentSkill> builder)
    {
        builder.ToTable("AgentSkills");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.AgentId, s.SkillId }).IsUnique();
        builder.HasOne(s => s.Agent)
            .WithMany(a => a.Skills)
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Skill)
            .WithMany(skill => skill.Agents)
            .HasForeignKey(s => s.SkillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class McpServerConfiguration : IEntityTypeConfiguration<McpServer>
{
    public void Configure(EntityTypeBuilder<McpServer> builder)
    {
        builder.ToTable("McpServers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Endpoint).HasMaxLength(1000).IsRequired();
        builder.Property(m => m.Transport).HasMaxLength(32).IsRequired();
        builder.Property(m => m.AuthTokenProtected).HasMaxLength(4000);
    }
}

public class AgentMcpServerConfiguration : IEntityTypeConfiguration<AgentMcpServer>
{
    public void Configure(EntityTypeBuilder<AgentMcpServer> builder)
    {
        builder.ToTable("AgentMcpServers");
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => new { m.AgentId, m.McpServerId }).IsUnique();
        builder.HasOne(m => m.Agent)
            .WithMany(a => a.McpServers)
            .HasForeignKey(m => m.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.McpServer)
            .WithMany(server => server.Agents)
            .HasForeignKey(m => m.McpServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.TargetType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.TargetId).HasMaxLength(100);
        builder.Property(a => a.Actor).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Detail).HasMaxLength(8000);
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => new { a.ProjectId, a.CreatedAt });
    }
}
