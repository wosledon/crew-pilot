using CrewPilot.Agents.Abstractions;
using CrewPilot.Agents.Engine;
using CrewPilot.Agents.Execution;
using CrewPilot.Agents.Workflows;
using CrewPilot.Api.Auth;
using CrewPilot.Api.Providers;
using CrewPilot.Api.Realtime;
using CrewPilot.Core.Abstractions;
using CrewPilot.Infrastructure.Audit;
using CrewPilot.Infrastructure.Cicd;
using CrewPilot.Infrastructure.Persistence;
using CrewPilot.Infrastructure.Providers;
using CrewPilot.Infrastructure.Security;
using CrewPilot.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 生产环境输出结构化 JSON 日志（便于容器日志采集 / 检索）
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
}

// 数据访问：开发默认 SQLite；Database:Provider=PostgreSql 切换 PostgreSQL（独立迁移程序集）
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var usePostgreSql = databaseProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) ||
                    databaseProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase);
var connectionString = builder.Configuration.GetConnectionString("CrewPilot")
    ?? (usePostgreSql
        ? "Host=localhost;Port=5432;Database=crewpilot;Username=crewpilot;Password=crewpilot"
        : "Data Source=crewpilot.db");
builder.Services.AddDbContext<CrewPilotDbContext>(options =>
{
    if (usePostgreSql)
    {
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(
            "CrewPilot.Infrastructure.PostgreSql"));
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// 领域与基础设施服务
// 机密保护：Windows 用 DPAPI；非 Windows（Docker/Linux）用 AES-GCM + 本地密钥文件
if (OperatingSystem.IsWindows())
{
    #pragma warning disable CA1416
    builder.Services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
    #pragma warning restore CA1416
}
else
{
    var secretKeyPath = builder.Configuration["Secrets:KeyPath"];
    if (string.IsNullOrWhiteSpace(secretKeyPath))
    {
        secretKeyPath = Path.Combine(
            builder.Environment.ContentRootPath, "App_Data", "secrets.key");
    }

    builder.Services.AddSingleton<ISecretProtector>(new AesGcmSecretProtector(secretKeyPath));
}

// Agent 平台：MAF 引擎 + 后台执行队列（认领闭环）
builder.Services.AddSingleton<IAgentEngine, MafAgentEngine>();
builder.Services.AddSingleton<AgentRunQueue>();
builder.Services.AddSingleton<AgentRunCancellationRegistry>();
builder.Services.AddSingleton<AgentRunStreamHub>();
builder.Services.AddHostedService<AgentRunWorker>();
builder.Services.AddScoped<AgentRunExecutor>();
builder.Services.AddScoped<WorkflowRunner>();
builder.Services.AddScoped<AuditLogger>();
builder.Services.AddHttpClient("models.dev");
builder.Services.AddHttpClient("mcp");
builder.Services.AddHostedService<ProviderCatalogRefreshService>();

// 附件存储：本地目录（可用 Attachments:RootPath 覆盖）
var attachmentRoot = builder.Configuration["Attachments:RootPath"];
if (string.IsNullOrWhiteSpace(attachmentRoot))
{
    attachmentRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "attachments");
}

builder.Services.AddSingleton<IAttachmentStorage>(new LocalAttachmentStorage(attachmentRoot));

// Skill 资源目录（scripts/ + resources/）：可用 Skills:RootPath 覆盖
var skillsRoot = builder.Configuration["Skills:RootPath"];
if (string.IsNullOrWhiteSpace(skillsRoot))
{
    skillsRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "skills");
}

builder.Services.AddSingleton(new SkillStorage(skillsRoot));

// CI/CD：gh / glab CLI 封装（Token 由项目集成配置加密存储）
builder.Services.AddSingleton<IGitCliService, GitCliService>();

// 实时推送：SignalR Hub + 领域事件广播
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// 单用户认证：配置 Auth:Password 后启用 Cookie 登录（默认关闭，自托管开箱即用）
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
var authPassword = builder.Configuration["Auth:Password"];
if (!string.IsNullOrWhiteSpace(authPassword))
{
    builder.Services
        .AddAuthentication(
            Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults
                .AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = "crewpilot_auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
}

// CORS：开发期前端 dev server（Vite 默认 5173 / 预览 4173）
builder.Services.AddCors(options =>
    options.AddPolicy("FrontendDev", policy => policy
        .WithOrigins("http://localhost:5173", "http://localhost:4173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

// 自动应用迁移：开发环境默认开启；容器/生产可通过 Database:AutoMigrate=true 开启
var autoMigrate = app.Configuration.GetValue(
    "Database:AutoMigrate", app.Environment.IsDevelopment());
if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CrewPilotDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();

// 请求关联 ID：响应头返回，日志/排查可串联
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
    await next();
});

app.UseCors("FrontendDev");

if (!string.IsNullOrWhiteSpace(authPassword))
{
    app.UseAuthentication();
    app.UseMiddleware<AuthGateMiddleware>();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHub<RealtimeHub>("/hubs/realtime");

// 存活探针：容器 / 反向代理健康检查（不访问数据库，避免抖动）
app.MapGet("/health", (IConfiguration configuration) => Results.Ok(new
{
    status = "ok",
    database = configuration["Database:Provider"] ?? "Sqlite",
}));

app.Run();

// 供集成测试（WebApplicationFactory<Program>）引用
public partial class Program;
