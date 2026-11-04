using CrewPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CrewPilot.Infrastructure.PostgreSql;

/// <summary>
/// PostgreSQL 迁移的设计时工厂：迁移文件只放在本程序集，
/// 避免与 SQLite 迁移（CrewPilot.Infrastructure）混用。
/// </summary>
public sealed class PostgreSqlDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<CrewPilotDbContext>
{
    public CrewPilotDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CrewPilotDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=crewpilot;Username=crewpilot;Password=crewpilot",
                builder => builder.MigrationsAssembly(
                    typeof(PostgreSqlDesignTimeDbContextFactory).Assembly.FullName))
            .Options;
        return new CrewPilotDbContext(options);
    }
}
