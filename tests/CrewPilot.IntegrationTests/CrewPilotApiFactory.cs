using CrewPilot.Agents.Abstractions;
using CrewPilot.Infrastructure.Cicd;
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
///
/// 注意：每个 DbContext 使用独立连接（连接串指向同一共享内存库），
/// 而不是共享同一个 SqliteConnection 实例——后台 AgentRun Worker 与 HTTP 请求会并发访问数据库，
/// 共享连接实例会触发 "unable to delete/modify user-function due to active statements"。
/// </summary>
public sealed class CrewPilotApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _attachmentRoot = Path.Combine(
        Path.GetTempPath(), "crewpilot_attachments_" + Guid.NewGuid().ToString("N"));

    // 保持一个连接打开，确保共享内存数据库在工厂生命周期内不销毁
    private readonly SqliteConnection _keepAliveConnection;

    public CrewPilotApiFactory()
    {
        var databaseName = "crewpilot_it_" + Guid.NewGuid().ToString("N");
        _connectionString =
            $"DataSource=file:{databaseName}?mode=memory&cache=shared;Default Timeout=30;Pooling=False";
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
        Directory.CreateDirectory(_attachmentRoot);
    }

    /// <summary>测试可直接操作 Fake CI/CD 服务（每个工厂实例独立）。</summary>
    public FakeGitCliService GitCli =>
        (FakeGitCliService)Services.GetRequiredService<IGitCliService>();

    /// <summary>测试可直接读取 Fake Agent 引擎（例如断言提示词注入）。</summary>
    public FakeAgentEngine Engine =>
        (FakeAgentEngine)Services.GetRequiredService<IAgentEngine>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Attachments:RootPath", _attachmentRoot);

        builder.ConfigureServices(services =>
        {
            // 用共享内存库替换默认的 SQLite 文件库
            var descriptor = services.Single(d =>
                d.ServiceType == typeof(DbContextOptions<CrewPilotDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<CrewPilotDbContext>(options =>
                options.UseSqlite(_connectionString));

            // 替换 Agent 引擎为 Fake（不发起真实模型调用）
            services.RemoveAll<IAgentEngine>();
            services.AddSingleton<IAgentEngine, FakeAgentEngine>();

            // 替换 CI/CD CLI 为 Fake（不调用真实 gh/glab）
            services.RemoveAll<IGitCliService>();
            services.AddSingleton<IGitCliService, FakeGitCliService>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _keepAliveConnection.Dispose();
        try
        {
            if (Directory.Exists(_attachmentRoot))
            {
                Directory.Delete(_attachmentRoot, recursive: true);
            }
        }
        catch
        {
            // 临时目录清理失败不影响测试结果
        }
    }
}
