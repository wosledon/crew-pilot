namespace CrewPilot.Agents.Abstractions;

using System.Runtime.CompilerServices;

/// <summary>
/// Agent 执行引擎抽象。
/// 约定：所有对 Microsoft Agent Framework（MAF）的调用必须收敛于此接口的实现之后，
/// 禁止业务代码直接依赖框架 API，便于框架升级时替换实现（见 AGENTS.md 第 6 节）。
/// </summary>
public interface IAgentEngine
{
    Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式执行：增量输出（Delta）与最终结果（Result）。
    /// 默认实现回退到一次性执行并按块产出，具体引擎可覆盖为真实 token 流。
    /// </summary>
    async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        AgentRunRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(request, cancellationToken);
        const int chunkSize = 32;
        for (var offset = 0; offset < result.Output.Length; offset += chunkSize)
        {
            yield return new AgentStreamEvent(
                result.Output.Substring(offset, Math.Min(chunkSize, result.Output.Length - offset)));
        }

        yield return new AgentStreamEvent(Result: result);
    }
}

/// <summary>流式执行事件：Delta 为增量文本，Result 为最终执行结果（二者互斥）。</summary>
public sealed record AgentStreamEvent(string? Delta = null, AgentRunResult? Result = null);

/// <summary>一次 Agent 执行请求。</summary>
public sealed record AgentRunRequest(
    string AgentName,
    string Prompt,
    string? SystemPrompt = null,
    string? Model = null,
    string? BaseUrl = null,
    string? ApiKey = null,
    string? WorkingDirectory = null,
    IReadOnlyList<string>? Tools = null,
    IReadOnlyList<string>? CommandWhitelist = null,
    IReadOnlyList<McpToolDescriptor>? McpTools = null,
    IMcpToolInvoker? McpInvoker = null,
    CrewPilot.Core.Agents.SandboxPolicy SandboxPolicy =
        CrewPilot.Core.Agents.SandboxPolicy.WorkspaceWrite);

/// <summary>执行过程中的一个步骤（工具调用 / 助手输出 / 错误）。</summary>
public sealed record AgentRunStep(int Sequence, string Kind, string Content);

/// <summary>
/// 一次 Agent 执行结果：输出、耗时与 token 成本（工时 1:1 折算与成本统计的数据来源）。
/// </summary>
public sealed record AgentRunResult(
    bool Succeeded,
    string Output,
    string? Error = null,
    TimeSpan Duration = default,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    IReadOnlyList<AgentRunStep>? Steps = null);
