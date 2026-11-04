using CrewPilot.Core.Agents;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Integrations;
using CrewPilot.Core.Iterations;
using CrewPilot.Core.Projects;
using CrewPilot.Core.Prototypes;
using CrewPilot.Core.Providers;
using CrewPilot.Core.WorkItems;
using CrewPilot.Core.WorkLogs;
using CrewPilot.Core.Workflows;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Infrastructure.Persistence;

/// <summary>Crew Pilot 主 DbContext（EF Core，开发期 SQLite / 生产 PostgreSQL）。</summary>
public class CrewPilotDbContext(DbContextOptions<CrewPilotDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> Members => Set<ProjectMember>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();

    public DbSet<Requirement> Requirements => Set<Requirement>();

    public DbSet<Defect> Defects => Set<Defect>();

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    public DbSet<Iteration> Iterations => Set<Iteration>();

    public DbSet<Milestone> Milestones => Set<Milestone>();

    public DbSet<Comment> Comments => Set<Comment>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<Activity> Activities => Set<Activity>();

    public DbSet<WorkLog> WorkLogs => Set<WorkLog>();

    public DbSet<Prototype> Prototypes => Set<Prototype>();

    public DbSet<ModelProvider> ModelProviders => Set<ModelProvider>();

    public DbSet<ModelCatalogEntry> ModelCatalogEntries => Set<ModelCatalogEntry>();

    public DbSet<Agent> Agents => Set<Agent>();

    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    public DbSet<RunLog> RunLogs => Set<RunLog>();

    public DbSet<AgentTeam> AgentTeams => Set<AgentTeam>();

    public DbSet<AgentTeamMember> AgentTeamMembers => Set<AgentTeamMember>();

    public DbSet<Workflow> Workflows => Set<Workflow>();

    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();

    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();

    public DbSet<Blocker> Blockers => Set<Blocker>();

    public DbSet<ProjectIntegration> ProjectIntegrations => Set<ProjectIntegration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrewPilotDbContext).Assembly);
    }
}
