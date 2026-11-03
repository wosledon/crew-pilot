namespace CrewPilot.Agents.Abstractions;

/// <summary>
/// Agent 执行引擎抽象。
/// 约定：所有对 Microsoft Agent Framework（MAF）的调用必须收敛于此接口的实现之后，
/// 禁止业务代码直接依赖框架 API，便于框架升级时替换实现（见 AGENTS.md 第 6 节）。
/// </summary>
public interface IAgentEngine
{
    Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken = default);
}

/// <summary>一次 Agent 执行请求。</summary>
public sealed record AgentRunRequest(
    string AgentName,
    string Prompt,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Variables = null);

/// <summary>
/// 一次 Agent 执行结果：输出、耗时与 token 成本（工时 1:1 折算与成本统计的数据来源）。
/// </summary>
public sealed record AgentRunResult(
    bool Succeeded,
    string Output,
    string? Error = null,
    TimeSpan Duration = default,
    int? PromptTokens = null,
    int? CompletionTokens = null);
