using System.ClientModel;
using CrewPilot.Agents.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace CrewPilot.Agents.Engine;

/// <summary>
/// 基于 Microsoft Agent Framework 的 AgentEngine 实现（OpenAI 兼容端点）。
/// 所有 MAF 调用收敛于此（见 AGENTS.md 第 6 节）。
/// </summary>
public sealed class MafAgentEngine : IAgentEngine
{
    public async Task<AgentRunResult> RunAsync(
        AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var steps = new List<AgentRunStep>();
        var sequence = 0;
        void Log(string kind, string content) =>
            steps.Add(new AgentRunStep(++sequence, kind, content));

        if (string.IsNullOrWhiteSpace(request.BaseUrl) ||
            string.IsNullOrWhiteSpace(request.ApiKey) ||
            string.IsNullOrWhiteSpace(request.Model))
        {
            const string message = "缺少模型供应商配置（BaseUrl / ApiKey / Model）";
            Log("error", message);
            return new AgentRunResult(false, string.Empty, message, TimeSpan.Zero, Steps: steps);
        }

        try
        {
            var openAiClient = new OpenAIClient(
                new ApiKeyCredential(request.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(request.BaseUrl) });

            var tools = AgentTools.Create(request.WorkingDirectory, request.CommandWhitelist);
            Log("info", $"Agent {request.AgentName} 启动（模型 {request.Model}，工具 {tools.Count} 个）");

            var agent = openAiClient
                .GetChatClient(request.Model)
                .AsIChatClient()
                .AsAIAgent(
                    instructions: request.SystemPrompt,
                    name: request.AgentName,
                    tools: tools);

            var response = await agent.RunAsync(request.Prompt, cancellationToken: cancellationToken);
            var output = response.Text ?? string.Empty;
            Log("assistant", output);

            var usage = response.Usage;
            int? promptTokens = (int?)usage?.InputTokenCount;
            int? completionTokens = (int?)usage?.OutputTokenCount;
            return new AgentRunResult(
                true,
                output,
                null,
                DateTimeOffset.UtcNow - started,
                promptTokens,
                completionTokens,
                steps);
        }
        catch (Exception ex)
        {
            Log("error", ex.Message);
            return new AgentRunResult(
                false,
                string.Empty,
                ex.Message,
                DateTimeOffset.UtcNow - started,
                Steps: steps);
        }
    }
}
