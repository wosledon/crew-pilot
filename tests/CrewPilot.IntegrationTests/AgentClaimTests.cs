using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Agents;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

/// <summary>Agent 认领闭环：指派 → 后台执行（Fake 引擎）→ 工时折算 / 活动流 / PR 回填。</summary>
public sealed class AgentClaimTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task AssignWorkItemToAgent_RunsToSuccess_AndLogsHours()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var provider = await CreateProviderAsync(client);
        var agent = await CreateAgentAsync(client, provider.Id);
        var workItem = await CreateRequirementAsync(client, project.Id);

        var assign = await client.PostAsJsonAsync(
            $"/api/workitems/{workItem.Id}/assign-agent", new { agentId = agent.Id });
        assign.StatusCode.Should().Be(HttpStatusCode.Created);
        var runSummary = await assign.Content.ReadFromJsonAsync<AgentRunListItemDto>();
        runSummary!.Status.Should().Be("queued");

        // 等待后台 Worker 执行完成（Fake 引擎瞬时返回，90 分钟耗时 → 1.5 小时）
        var detail = await WaitForTerminalAsync(client, runSummary.Id);

        detail!.Status.Should().Be("succeeded");
        detail.Output.Should().Contain(FakeAgentEngine.FakePrUrl);
        detail.Logs.Should().NotBeEmpty();
        detail.PromptTokens.Should().Be(1200);

        // PR 链接回填
        var updated = await client.GetFromJsonAsync<WorkItemDetailDto>($"/api/workitems/{workItem.Id}");
        updated!.PrUrl.Should().Be(FakeAgentEngine.FakePrUrl);

        // 工时 1:1 折算：90 分钟 → 1.5 小时
        var logs = await client.GetFromJsonAsync<List<WorkLogDto>>(
            $"/api/workitems/{workItem.Id}/worklogs");
        logs!.Should().ContainSingle(l => l.Hours == 1.5m && l.AgentRunId != null);
    }

    [Fact]
    public async Task AssignWorkItem_ToUnknownAgent_Returns404()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var workItem = await CreateRequirementAsync(client, project.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/workitems/{workItem.Id}/assign-agent", new { agentId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PreviewAgent_CreatesQueuedRun()
    {
        var client = factory.CreateClient();
        var provider = await CreateProviderAsync(client);
        var agent = await CreateAgentAsync(client, provider.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/agents/{agent.Id}/runs", new { prompt = "帮我看看这个项目" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<AgentRunListItemDto>();
        dto!.Trigger.Should().Be("preview");
        dto.Status.Should().Be("queued");
    }

    [Fact]
    public async Task RetryFailedRun_CreatesNewQueuedRun()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var provider = await CreateProviderAsync(client);
        var agent = await CreateAgentAsync(client, provider.Id);
        var workItem = await CreateRequirementAsync(client, project.Id);
        factory.Engine.FailNextRuns = 1;

        var assign = await client.PostAsJsonAsync(
            $"/api/workitems/{workItem.Id}/assign-agent", new { agentId = agent.Id });
        assign.StatusCode.Should().Be(HttpStatusCode.Created);
        var first = await assign.Content.ReadFromJsonAsync<AgentRunListItemDto>();
        var failed = await WaitForTerminalAsync(client, first!.Id);
        failed!.Status.Should().Be("failed");

        var retry = await client.PostAsync($"/api/agent-runs/{first.Id}/retry", null);
        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        var retried = await retry.Content.ReadFromJsonAsync<AgentRunListItemDto>();
        retried!.Trigger.Should().Be("retry");
        retried.Id.Should().NotBe(first.Id);

        var succeeded = await WaitForTerminalAsync(client, retried.Id);
        succeeded!.Status.Should().Be("succeeded");

        var runs = await client.GetFromJsonAsync<List<AgentRunListItemDto>>(
            $"/api/agent-runs?workItemId={workItem.Id}");
        runs!.Should().HaveCount(2);
        runs.Should().Contain(r => r.Id == first.Id && r.Status == "failed");
    }

    [Fact]
    public async Task RetrySucceededRun_Returns400()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var provider = await CreateProviderAsync(client);
        var agent = await CreateAgentAsync(client, provider.Id);
        var workItem = await CreateRequirementAsync(client, project.Id);

        var assign = await client.PostAsJsonAsync(
            $"/api/workitems/{workItem.Id}/assign-agent", new { agentId = agent.Id });
        var run = await assign.Content.ReadFromJsonAsync<AgentRunListItemDto>();
        var terminal = await WaitForTerminalAsync(client, run!.Id);
        terminal!.Status.Should().Be("succeeded");

        var retry = await client.PostAsync($"/api/agent-runs/{run.Id}/retry", null);
        retry.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SandboxPolicy_IsPersistedAndPassedToEngine()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var provider = await CreateProviderAsync(client);
        var agentResponse = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "只读 Agent" + Guid.NewGuid().ToString("N")[..6],
            job = "developer",
            providerId = provider.Id,
            model = "gpt-4o-mini",
            systemPrompt = "只读分析",
            sandboxPolicy = "readonly",
        });
        agentResponse.EnsureSuccessStatusCode();
        var agent = await agentResponse.Content.ReadFromJsonAsync<AgentDetailDto>();
        agent!.SandboxPolicy.Should().Be("readonly");

        var workItem = await CreateRequirementAsync(client, project.Id);
        var assign = await client.PostAsJsonAsync(
            $"/api/workitems/{workItem.Id}/assign-agent", new { agentId = agent.Id });
        var run = await assign.Content.ReadFromJsonAsync<AgentRunListItemDto>();
        var detail = await WaitForTerminalAsync(client, run!.Id);

        detail!.Status.Should().Be("succeeded");
        factory.Engine.LastRequest!.SandboxPolicy.Should().Be(SandboxPolicy.ReadOnly);
    }

    private static async Task<AgentRunDetailDto?> WaitForTerminalAsync(
        HttpClient client, Guid runId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var detail = await client.GetFromJsonAsync<AgentRunDetailDto>($"/api/agent-runs/{runId}");
            if (detail is { Status: "succeeded" or "failed" or "canceled" })
            {
                return detail;
            }

            await Task.Delay(100);
        }

        return null;
    }

    private static async Task<ProjectDetailDto> CreateProjectAsync(HttpClient client)
    {
        var key = "A" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var response = await client.PostAsJsonAsync("/api/projects", new { key, name = "认领闭环项目" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDetailDto>())!;
    }

    private static async Task<ProviderDetailDto> CreateProviderAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/providers", new
        {
            name = "认领供应商" + Guid.NewGuid().ToString("N")[..6],
            baseUrl = "https://api.example.com/v1",
            apiKey = "sk-claim",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProviderDetailDto>())!;
    }

    private static async Task<AgentDetailDto> CreateAgentAsync(HttpClient client, Guid providerId)
    {
        var response = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "认领 Agent" + Guid.NewGuid().ToString("N")[..6],
            job = "developer",
            providerId,
            model = "gpt-4o-mini",
            systemPrompt = "你是一名资深开发者",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentDetailDto>())!;
    }

    private static async Task<WorkItemDetailDto> CreateRequirementAsync(HttpClient client, Guid projectId)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/workitems", new
        {
            type = "requirement",
            title = "认领闭环需求",
            estimatedHours = 4m,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkItemDetailDto>())!;
    }
}
