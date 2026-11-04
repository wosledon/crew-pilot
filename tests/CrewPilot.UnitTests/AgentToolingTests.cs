using CrewPilot.Agents.Abstractions;
using CrewPilot.Agents.Engine;
using CrewPilot.Agents.Execution;
using CrewPilot.Agents.Workflows;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Agents;
using CrewPilot.Core.Projects;
using CrewPilot.Core.Providers;
using CrewPilot.Core.WorkItems;
using CrewPilot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrewPilot.UnitTests;

/// <summary>Agent 工具：路径沙箱、命令拆分与工具白名单。</summary>
public sealed class AgentToolingTests
{
    [Fact]
    public void ResolvePath_AllowsPathInsideWorkingDirectory()
    {
        var root = Directory.CreateTempSubdirectory("crewpilot_tools_").FullName;
        try
        {
            var resolved = AgentTools.ResolvePath(root, Path.Combine("src", "Program.cs"));

            resolved.Should().NotBeNull();
            resolved!.Should().StartWith(Path.GetFullPath(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolvePath_RejectsSiblingDirectoryWithSamePrefix()
    {
        var root = Directory.CreateTempSubdirectory("crewpilot_tools_").FullName;
        var sibling = root + "-evil";
        Directory.CreateDirectory(sibling);
        try
        {
            var escaped = Path.Combine("..", Path.GetFileName(sibling), "secret.txt");

            AgentTools.ResolvePath(root, escaped).Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(sibling, recursive: true);
        }
    }

    [Fact]
    public void ResolvePath_RejectsAbsolutePathOutsideWorkingDirectory()
    {
        var root = Directory.CreateTempSubdirectory("crewpilot_tools_").FullName;
        try
        {
            var outside = Path.Combine(Path.GetTempPath(), "win.ini");

            AgentTools.ResolvePath(root, outside).Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TokenizeCommand_SplitsArgumentsAndKeepsQuotedValues()
    {
        var tokens = AgentTools.TokenizeCommand("git commit -m \"fix: 中文 消息\"");

        tokens.Should().Equal("git", "commit", "-m", "fix: 中文 消息");
    }

    [Fact]
    public void TokenizeCommand_RejectsShellChainsBySplittingThemAsArguments()
    {
        // 不经过 shell：&& 只是普通参数，不会被解释为第二条命令
        var tokens = AgentTools.TokenizeCommand("git status && curl http://evil");

        tokens.Should().Equal("git", "status", "&&", "curl", "http://evil");
        tokens[0].Should().Be("git");
    }

    [Fact]
    public void Create_OnlyEnablesConfiguredTools()
    {
        var root = Directory.CreateTempSubdirectory("crewpilot_tools_").FullName;
        try
        {
            var tools = AgentTools.Create(root, ["read_file", "list_files"]);

            tools.Select(tool => tool.Name)
                .Should().BeEquivalentTo(["read_file", "list_files"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Create_ReadOnlyPolicy_RemovesWriteAndCommandTools()
    {
        var root = Directory.CreateTempSubdirectory("crewpilot_tools_").FullName;
        try
        {
            var tools = AgentTools.Create(
                root,
                null,
                ["git"],
                SandboxPolicy.ReadOnly);

            tools.Select(tool => tool.Name)
                .Should().BeEquivalentTo(["read_file", "list_files"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Create_TrustedPolicy_AllowsCommandsWithoutWhitelist()
    {
        var root = Directory.CreateTempSubdirectory("crewpilot_tools_").FullName;
        try
        {
            var tools = AgentTools.Create(
                root,
                null,
                null,
                SandboxPolicy.Trusted);

            tools.Select(tool => tool.Name)
                .Should().Contain("run_command");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Create_DefaultsToFileTools_AndRunCommandRequiresWhitelist()
    {
        var root = Directory.CreateTempSubdirectory("crewpilot_tools_").FullName;
        try
        {
            AgentTools.Create(root).Select(tool => tool.Name)
                .Should().BeEquivalentTo(["read_file", "write_file", "list_files"]);

            AgentTools.Create(root, null, ["git"]).Select(tool => tool.Name)
                .Should().Contain("run_command");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Create_ReturnsNoToolsWithoutValidWorkingDirectory()
    {
        AgentTools.Create(null).Should().BeEmpty();
        AgentTools.Create(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).Should().BeEmpty();
    }
}

/// <summary>AgentRun 取消语义：运行中可中止、排队取消不执行、终态不再取消。</summary>
public sealed class AgentRunCancellationTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCanceled_StopsEngineAndMarksRunCanceled()
    {
        await using var db = await CreateDbAsync();
        var (run, engine, executor, registry) = await SeedRunAsync(db, AgentRunStatus.Queued);

        var execution = executor.ExecuteAsync(run.Id);
        await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        registry.Cancel(run.Id).Should().BeTrue();

        await execution.WaitAsync(TimeSpan.FromSeconds(5));
        run.Status.Should().Be(AgentRunStatus.Canceled);
        run.FinishedAt.Should().NotBeNull();
        run.Error.Should().Contain("取消");
    }

    [Fact]
    public async Task ExecuteAsync_WithQueuedCanceledRun_DoesNotInvokeEngine()
    {
        await using var db = await CreateDbAsync();
        var (run, engine, executor, _) = await SeedRunAsync(db, AgentRunStatus.Queued);

        (await executor.CancelAsync(run.Id)).Should().BeTrue();
        await executor.ExecuteAsync(run.Id);

        engine.CallCount.Should().Be(0);
        run.Status.Should().Be(AgentRunStatus.Canceled);
    }

    [Fact]
    public async Task CancelAsync_ForFinishedRun_ReturnsFalse()
    {
        await using var db = await CreateDbAsync();
        var (run, _, executor, _) = await SeedRunAsync(db, AgentRunStatus.Succeeded);

        (await executor.CancelAsync(run.Id)).Should().BeFalse();
        run.Status.Should().Be(AgentRunStatus.Succeeded);
    }

    private static async Task<CrewPilotDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CrewPilotDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new CrewPilotDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<(AgentRun Run, BlockingEngine Engine, AgentRunExecutor Executor, AgentRunCancellationRegistry Registry)>
        SeedRunAsync(CrewPilotDbContext db, AgentRunStatus status)
    {
        var provider = new ModelProvider
        {
            Name = "test",
            BaseUrl = "http://localhost/v1",
            ApiKeyProtected = "test-key",
        };
        var agent = new Agent
        {
            Name = "开发 Agent",
            Job = AgentJob.Developer,
            Provider = provider,
            Model = "test-model",
            SystemPrompt = "test",
        };
        var project = new Project { Key = "T" + Guid.NewGuid().ToString("N")[..6], Name = "测试项目" };
        var workItem = new Requirement { Project = project, Title = "测试需求" };
        var run = new AgentRun
        {
            Agent = agent,
            WorkItem = workItem,
            Prompt = "完成需求",
            Status = status,
        };

        db.AddRange(provider, agent, project, workItem, run);
        await db.SaveChangesAsync();

        var engine = new BlockingEngine();
        var registry = new AgentRunCancellationRegistry();
        var workflowRunner = new WorkflowRunner(
            db, new AgentRunQueue(), NullLogger<WorkflowRunner>.Instance);
        var executor = new AgentRunExecutor(
            db, engine, new PassThroughSecretProtector(), registry, workflowRunner,
            new AgentRunStreamHub());
        return (run, engine, executor, registry);
    }

    private sealed class BlockingEngine : IAgentEngine
    {
        private int _callCount;

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount => _callCount;

        public async Task<AgentRunResult> RunAsync(
            AgentRunRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new AgentRunResult(true, "unreachable");
        }
    }

    private sealed class PassThroughSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;

        public string Unprotect(string protectedText) => protectedText;
    }
}
