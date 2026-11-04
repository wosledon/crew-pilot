using System.Collections.Concurrent;
using CrewPilot.Agents.Abstractions;

namespace CrewPilot.IntegrationTests;

/// <summary>
/// 测试用 Fake 引擎：不发起真实模型调用。
/// 支持按顺序注入输出（用于工作流卡点场景）与注入失败次数（用于重试场景）。
/// </summary>
public sealed class FakeAgentEngine : IAgentEngine
{
    public const string FakePrUrl = "https://github.com/example/crew-pilot/pull/42";

    private readonly ConcurrentQueue<string> _outputs = new();
    private int _failNextRuns;

    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(90);

    /// <summary>最近一次执行请求（用于断言提示词/Skill 注入）。</summary>
    public AgentRunRequest? LastRequest { get; private set; }

    public string DefaultOutput { get; set; } = $"已完成工作，PR：{FakePrUrl}";

    public int FailNextRuns
    {
        get => Volatile.Read(ref _failNextRuns);
        set => Interlocked.Exchange(ref _failNextRuns, value);
    }

    /// <summary>预置下一次（或多次）执行的输出。</summary>
    public FakeAgentEngine EnqueueOutput(string output)
    {
        _outputs.Enqueue(output);
        return this;
    }

    public Task<AgentRunResult> RunAsync(
        AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        IReadOnlyList<AgentRunStep> steps =
        [
            new AgentRunStep(1, "info", $"Fake engine 启动：{request.AgentName}"),
            new AgentRunStep(2, "assistant", "fake run"),
        ];

        if (Interlocked.Decrement(ref _failNextRuns) >= 0)
        {
            return Task.FromResult(new AgentRunResult(
                false, string.Empty, "Fake 引擎模拟失败", Duration, null, null, steps));
        }

        var output = _outputs.TryDequeue(out var custom) ? custom : DefaultOutput;
        return Task.FromResult(new AgentRunResult(
            true, output, null, Duration, 1200, 300, steps));
    }
}
