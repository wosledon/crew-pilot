using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrewPilot.IntegrationTests;

/// <summary>
/// 集成测试工厂：共享内存 SQLite + 自动迁移，不依赖真实文件数据库。
/// </summary>
public sealed class CrewPilotApiFactory : WebApplicationFactory<Program>
{
    private const string ConnectionString = "DataSource=file:crewpilot_it?mode=memory&cache=shared";
    private readonly SqliteConnection _connection = new(ConnectionString);

    public CrewPilotApiFactory()
    {
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
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
