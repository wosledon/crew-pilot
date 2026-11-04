using System.Collections.Concurrent;
using CrewPilot.Agents.Execution;
using CrewPilot.Infrastructure.Mcp;
using FluentAssertions;
using Xunit;

namespace CrewPilot.UnitTests;

public sealed class McpRouteInvokerTests
{
    [Fact]
    public async Task CallTool_RecordsSuccessfulAudit()
    {
        var sink = new ConcurrentQueue<McpToolCallAudit>();
        var session = new FakeSession("工具结果");
        var invoker = new McpRouteInvoker(
            new Dictionary<string, McpRoute>
            {
                ["mcp_demo_echo"] = new(session, "echo", "demo", "echo"),
            },
            sink);

        var result = await invoker.CallToolAsync("mcp_demo_echo", """{"text":"hi"}""");

        result.Should().Be("工具结果");
        session.LastArguments.Should().Be("""{"text":"hi"}""");
        sink.Should().ContainSingle();
        var audit = sink.Single();
        audit.ServerName.Should().Be("demo");
        audit.ToolName.Should().Be("echo");
        audit.QualifiedName.Should().Be("mcp_demo_echo");
        audit.Succeeded.Should().BeTrue();
        audit.Error.Should().BeNull();
    }

    [Fact]
    public async Task CallTool_RecordsFailedAudit_AndRethrows()
    {
        var sink = new ConcurrentQueue<McpToolCallAudit>();
        var invoker = new McpRouteInvoker(
            new Dictionary<string, McpRoute>
            {
                ["mcp_demo_boom"] = new(
                    new FakeSession(new InvalidOperationException("连接中断"), "boom"),
                    "boom", "demo", "boom"),
            },
            sink);

        var act = () => invoker.CallToolAsync("mcp_demo_boom", null);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("连接中断");
        sink.Should().ContainSingle();
        var audit = sink.Single();
        audit.Succeeded.Should().BeFalse();
        audit.Error.Should().Be("连接中断");
    }

    [Fact]
    public async Task UnknownTool_ReturnsMessage_WithoutAudit()
    {
        var sink = new ConcurrentQueue<McpToolCallAudit>();
        var invoker = new McpRouteInvoker(
            new Dictionary<string, McpRoute>(), sink);

        var result = await invoker.CallToolAsync("missing", null);

        result.Should().Contain("未知 MCP 工具");
        sink.Should().BeEmpty();
    }

    [Theory]
    [InlineData("mcp_demo_echo", "mcp_demo_echo")]
    [InlineData("mcp.demo/echo", "mcp_demo_echo")]
    [InlineData("a b c", "a_b_c")]
    public void SanitizeToolName_ReplacesInvalidCharacters(
        string input, string expected)
    {
        McpRouteInvoker.SanitizeToolName(input).Should().Be(expected);
    }

    private sealed class FakeSession : IMcpSession
    {
        private readonly object? _result;
        private readonly string? _toolName;

        public FakeSession(string result)
        {
            _result = result;
        }

        public FakeSession(Exception error, string toolName)
        {
            _result = error;
            _toolName = toolName;
        }

        public IReadOnlyList<McpToolInfo> Tools => [];

        public string? LastArguments { get; private set; }

        public Task<string> CallToolAsync(
            string toolName, string? argumentsJson, CancellationToken ct = default)
        {
            LastArguments = argumentsJson;
            return _result is Exception error
                ? Task.FromException<string>(error)
                : Task.FromResult((string)_result!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
