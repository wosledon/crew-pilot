using CrewPilot.Agents.Abstractions;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrewPilot.IntegrationTests;

/// <summary>
/// 集成测试工厂：共享内存 SQLite + 自动迁移，不依赖真实文件数据库。
/// 用 FakeAgentEngine 替换 MAF 引擎，避免真实模型调用。
/// </summary>
public sealed class CrewPilotApiFactory : WebApplicationFactory<Program>
{
    // 每个工厂实例使用独立的内存库，避免 xUnit 并行测试类之间的 SQLite 锁竞争
    private readonly string _databaseName = "crewpilot_it_" + Guid.NewGuid().ToString("N");
    private readonly SqliteConnection _connection;

    public CrewPilotApiFactory()
    {
        _connection = new SqliteConnection($"DataSource=file:{_databaseName}?mode=memory&cache=shared");
        // 保持连接打开，共享内存数据库随工厂生命周期存在
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // 用共享内存库替换默认的 SQLite 文件库
            var descriptor = services.Single(d =>
                d.ServiceType == typeof(DbContextOptions<CrewPilotDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<CrewPilotDbContext>(options =>
                options.UseSqlite(_connection));

            // 替换 Agent 引擎为 Fake（不发起真实模型调用）
            services.RemoveAll<IAgentEngine>();
            services.AddSingleton<IAgentEngine, FakeAgentEngine>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
