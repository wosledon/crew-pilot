namespace CrewPilot.Agents.Abstractions;

/// <summary>暴露给模型的 MCP 工具描述（名称已按函数名约束清洗并去重）。</summary>
public sealed record McpToolDescriptor(string Name, string? Description, string? InputSchemaJson);

/// <summary>MCP 工具调用入口（由执行器按 Agent 绑定的 MCP Server 构造）。</summary>
public interface IMcpToolInvoker
{
    Task<string> CallToolAsync(
        string toolName, string? argumentsJson, CancellationToken cancellationToken = default);
}
