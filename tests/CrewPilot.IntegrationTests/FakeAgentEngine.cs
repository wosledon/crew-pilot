using CrewPilot.Agents.Abstractions;

namespace CrewPilot.IntegrationTests;

/// <summary>测试用 Fake 引擎：不发起真实模型调用，返回固定结果（含 PR 链接与 90 分钟耗时）。</summary>
public sealed class FakeAgentEngine : IAgentEngine
{
    public const string FakePrUrl = "https://github.com/example/crew-pilot/pull/42";

    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(90);

    public bool Succeed { get; init; } = true;

    public Task<AgentRunResult> RunAsync(
        AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AgentRunStep> steps =
        [
            new AgentRunStep(1, "info", $"Fake engine 启动：{request.AgentName}"),
            new AgentRunStep(2, "assistant", $"已完成工作，PR：{FakePrUrl}"),
        ];

        var result = Succeed
            ? new AgentRunResult(true, $"已完成工作，PR：{FakePrUrl}", null, Duration, 1200, 300, steps)
            : new AgentRunResult(false, string.Empty, "Fake 引擎模拟失败", Duration, null, null, steps);
        return Task.FromResult(result);
    }
}
