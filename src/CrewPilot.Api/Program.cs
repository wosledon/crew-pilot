using CrewPilot.Agents.Abstractions;
using CrewPilot.Agents.Engine;
using CrewPilot.Agents.Execution;
using CrewPilot.Agents.Workflows;
using CrewPilot.Core.Abstractions;
using CrewPilot.Infrastructure.Persistence;
using CrewPilot.Infrastructure.Security;
using CrewPilot.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 数据访问：开发期 SQLite，连接串可切换 PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("CrewPilot")
    ?? "Data Source=crewpilot.db";
builder.Services.AddDbContext<CrewPilotDbContext>(options =>
    options.UseSqlite(connectionString));

// 领域与基础设施服务
// DPAPI 为 Windows 专用实现（单用户自托管当前面向 Windows）；跨平台时替换实现即可
#pragma warning disable CA1416
builder.Services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
#pragma warning restore CA1416

// Agent 平台：MAF 引擎 + 后台执行队列（认领闭环）
builder.Services.AddSingleton<IAgentEngine, MafAgentEngine>();
builder.Services.AddSingleton<AgentRunQueue>();
builder.Services.AddSingleton<AgentRunCancellationRegistry>();
builder.Services.AddHostedService<AgentRunWorker>();
builder.Services.AddScoped<AgentRunExecutor>();
builder.Services.AddScoped<WorkflowRunner>();
builder.Services.AddHttpClient("models.dev");

// 附件存储：本地目录（可用 Attachments:RootPath 覆盖）
var attachmentRoot = builder.Configuration["Attachments:RootPath"];
if (string.IsNullOrWhiteSpace(attachmentRoot))
{
    attachmentRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "attachments");
}

builder.Services.AddSingleton<IAttachmentStorage>(new LocalAttachmentStorage(attachmentRoot));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// CORS：开发期前端 dev server（Vite 默认 5173 / 预览 4173）
builder.Services.AddCors(options =>
    options.AddPolicy("FrontendDev", policy => policy
        .WithOrigins("http://localhost:5173", "http://localhost:4173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// 开发环境自动应用迁移，保证开箱即用
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CrewPilotDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseCors("FrontendDev");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

// 供集成测试（WebApplicationFactory<Program>）引用
public partial class Program;
