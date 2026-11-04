using CrewPilot.Core.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CrewPilot.Infrastructure.Persistence.Configurations;

public class AgentTeamConfiguration : IEntityTypeConfiguration<AgentTeam>
{
    public void Configure(EntityTypeBuilder<AgentTeam> builder)
    {
        builder.ToTable("AgentTeams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Goal).HasMaxLength(2000);
        builder.HasOne(t => t.Project)
            .WithMany()
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Members)
            .WithOne(m => m.Team)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AgentTeamMemberConfiguration : IEntityTypeConfiguration<AgentTeamMember>
{
    public void Configure(EntityTypeBuilder<AgentTeamMember> builder)
    {
        builder.ToTable("AgentTeamMembers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Role).HasMaxLength(50);
        builder.HasIndex(m => new { m.TeamId, m.AgentId }).IsUnique();
        builder.HasOne(m => m.Agent)
            .WithMany()
            .HasForeignKey(m => m.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
