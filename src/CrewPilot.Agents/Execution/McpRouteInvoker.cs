using System.Diagnostics;
using System.Text.RegularExpressions;
using CrewPilot.Agents.Abstractions;
using CrewPilot.Core.Mcp;
using CrewPilot.Infrastructure.Mcp;

namespace CrewPilot.Agents.Execution;

/// <summary>一次 MCP 工具调用的审计记录（由运行器在结束后落库）。</summary>
public sealed record McpToolCallAudit(
    string ServerName,
    string ToolName,
    string QualifiedName,
    bool Succeeded,
    long ElapsedMilliseconds,
    string? Error,
    DateTimeOffset CalledAt);

/// <summary>MCP 工具路由：模型工具名 → 服务器连接 + 远端工具名。</summary>
public sealed record McpRoute(
    IMcpSession Client,
    string RemoteName,
    string ServerName,
    string ToolName);

/// <summary>
/// 带审计的 MCP 工具调用器：每次调用记录服务器/工具/耗时/结果，计入会话审计队列。
/// 工具调用失败时记录失败信息后继续向上抛出，由引擎按工具错误处理。
/// </summary>
public sealed class McpRouteInvoker(
    IReadOnlyDictionary<string, McpRoute> routes,
    System.Collections.Concurrent.ConcurrentQueue<McpToolCallAudit> auditSink)
    : IMcpToolInvoker
{
    private static readonly Regex InvalidToolNameChars = new(
        "[^a-zA-Z0-9_-]", RegexOptions.Compiled);

    public async Task<string> CallToolAsync(
        string toolName, string? argumentsJson, CancellationToken cancellationToken = default)
    {
        if (!routes.TryGetValue(toolName, out var route))
        {
            return $"未知 MCP 工具：{toolName}";
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await route.Client.CallToolAsync(
                route.RemoteName, argumentsJson, cancellationToken);
            stopwatch.Stop();
            auditSink.Enqueue(new McpToolCallAudit(
                route.ServerName,
                route.ToolName,
                toolName,
                Succeeded: true,
                stopwatch.ElapsedMilliseconds,
                Error: null,
                DateTimeOffset.UtcNow));
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            auditSink.Enqueue(new McpToolCallAudit(
                route.ServerName,
                route.ToolName,
                toolName,
                Succeeded: false,
                stopwatch.ElapsedMilliseconds,
                ex.Message,
                DateTimeOffset.UtcNow));
            throw;
        }
    }

    public static string SanitizeToolName(string combined)
    {
        var sanitized = InvalidToolNameChars.Replace(combined, "_");
        return sanitized.Length <= 64 ? sanitized : sanitized[..64];
    }
}
