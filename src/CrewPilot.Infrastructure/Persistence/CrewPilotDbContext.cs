using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Iterations;
using CrewPilot.Core.Projects;
using CrewPilot.Core.Prototypes;
using CrewPilot.Core.WorkItems;
using CrewPilot.Core.WorkLogs;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrewPilotDbContext).Assembly);
    }
}
