using System.Net;
using System.Net.Http.Json;
using CrewPilot.Agents.Abstractions;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class WorkflowsApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    private FakeAgentEngine Engine =>
        (FakeAgentEngine)factory.Services.GetRequiredService<IAgentEngine>();

    [Fact]
    public async Task AutoSteps_RunToCompletion_WithOneAgentRunPerStep()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);
        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var workflow = await CreateWorkflowAsync(client, project.Id, "两步自动流", new object[]
        {
            new { name = "步骤一", order = 0, agentId = agent.Id, approvalPolicy = "auto" },
            new { name = "步骤二", order = 1, agentId = agent.Id, approvalPolicy = "auto" },
        });

        var run = await StartRunAsync(client, workflow.Id, item.Id);
        var detail = await WaitForFinishedAsync(client, run.Id);

        detail.Run.Status.Should().Be("succeeded");
        detail.AgentRuns.Should().HaveCount(2);
    }

    [Fact]
    public async Task AlwaysApproval_CreatesBlocker_AndContinuesOnResolve()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);
        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var workflow = await CreateWorkflowAsync(client, project.Id, "审批流", new object[]
        {
            new
            {
                name = "发布审批",
                order = 0,
                agentId = agent.Id,
                approvalPolicy = "always",
            },
        });

        var run = await StartRunAsync(client, workflow.Id, item.Id);
        var blocker = await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<List<BlockerDto>>($"/api/projects/{project.Id}/blockers"),
            list => list is not null && list.Any(b => b.WorkflowRunId == run.Id));
        var approval = blocker!.Single(b => b.WorkflowRunId == run.Id);
        approval.Kind.Should().Be("approval");
        approval.Status.Should().Be("open");

        var resolve = await client.PostAsJsonAsync($"/api/blockers/{approval.Id}/resolve", new
        {
            resolution = "continue",
            note = "同意",
        });
        resolve.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await WaitForFinishedAsync(client, run.Id);
        detail.Run.Status.Should().Be("succeeded");
    }

    [Fact]
    public async Task AgentBlockerMarker_Suspends_AndRedirectRerunsStep()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);
        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        Engine.EnqueueOutput("范围不明确\n[[BLOCKER]] 请确认验收范围");

        var workflow = await CreateWorkflowAsync(client, project.Id, "卡点流", new object[]
        {
            new { name = "需求分析", order = 0, agentId = agent.Id, approvalPolicy = "auto" },
        });

        var run = await StartRunAsync(client, workflow.Id, item.Id);
        var blockers = await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<List<BlockerDto>>($"/api/projects/{project.Id}/blockers"),
            list => list is not null && list.Any(b => b.WorkflowRunId == run.Id && b.Kind == "agentblocked"));
        var blocker = blockers!.Single(b => b.WorkflowRunId == run.Id);
        blocker.Reason.Should().Contain("验收范围");

        var resolve = await client.PostAsJsonAsync($"/api/blockers/{blocker.Id}/resolve", new
        {
            resolution = "redirect",
            note = "按最小范围实现",
        });
        resolve.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await WaitForFinishedAsync(client, run.Id);
        detail.Run.Status.Should().Be("succeeded");
        detail.AgentRuns.Should().HaveCount(2);
    }

    [Fact]
    public async Task StepFailure_RetriesThenCreatesFailureBlocker()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);
        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        Engine.FailNextRuns = 2;

        var workflow = await CreateWorkflowAsync(client, project.Id, "重试流", new object[]
        {
            new
            {
                name = "开发实现",
                order = 0,
                agentId = agent.Id,
                approvalPolicy = "auto",
                maxRetries = 1,
            },
        });

        var run = await StartRunAsync(client, workflow.Id, item.Id);
        var blockers = await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<List<BlockerDto>>($"/api/projects/{project.Id}/blockers"),
            list => list is not null && list.Any(b => b.WorkflowRunId == run.Id && b.Kind == "failure"));
        var blocker = blockers!.Single(b => b.WorkflowRunId == run.Id);
        blocker.Reason.Should().Contain("重试耗尽");

        var detailBefore = await client.GetFromJsonAsync<WorkflowRunDetailDto>(
            $"/api/workflow-runs/{run.Id}");
        detailBefore!.AgentRuns.Should().HaveCount(2);

        await client.PostAsJsonAsync($"/api/blockers/{blocker.Id}/resolve", new
        {
            resolution = "continue",
            note = "继续",
        });

        var detail = await WaitForFinishedAsync(client, run.Id);
        detail.Run.Status.Should().Be("succeeded");
        detail.AgentRuns.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateFromTemplate_BuildsExpectedSteps()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        await ApiTestHelpers.CreateAgentAsync(client, provider.Id, "产品", "product");
        await ApiTestHelpers.CreateAgentAsync(client, provider.Id, "架构", "architect");
        await ApiTestHelpers.CreateAgentAsync(client, provider.Id, "开发", "developer");
        await ApiTestHelpers.CreateAgentAsync(client, provider.Id, "测试", "qa");

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/workflows/from-template",
            new { templateKey = "requirement-delivery" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var workflow = await response.Content.ReadFromJsonAsync<WorkflowDto>();

        workflow!.Steps.Should().HaveCount(7);
        workflow.Steps.Select(step => step.RequiredJob)
            .Should().Contain(["product", "architect", "developer", "qa"]);
        workflow.Steps.Last().ApprovalPolicy.Should().Be("always");
        workflow.Steps.Select(step => step.Order).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task StartRun_ForUnknownWorkItem_Returns400()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);
        var workflow = await CreateWorkflowAsync(client, project.Id, "空启动流", new object[]
        {
            new { name = "步骤", order = 0, agentId = agent.Id },
        });

        var response = await client.PostAsJsonAsync(
            $"/api/workflows/{workflow.Id}/runs", new { workItemId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ParallelSteps_StartTogether_ThenAdvanceAfterAllSucceed()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var first = await ApiTestHelpers.CreateAgentAsync(client, provider.Id, "并行 Agent 1");
        var second = await ApiTestHelpers.CreateAgentAsync(client, provider.Id, "并行 Agent 2");
        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var workflow = await CreateWorkflowAsync(client, project.Id, "并行流", new object[]
        {
            new { name = "并行步骤 A", order = 0, parallel = true, agentId = first.Id, approvalPolicy = "auto" },
            new { name = "并行步骤 B", order = 0, parallel = true, agentId = second.Id, approvalPolicy = "auto" },
            new { name = "汇总步骤", order = 1, agentId = first.Id, approvalPolicy = "auto" },
        });

        var run = await StartRunAsync(client, workflow.Id, item.Id);
        var started = await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<WorkflowRunDetailDto>($"/api/workflow-runs/{run.Id}"),
            detail => detail is not null && detail.AgentRuns.Count >= 2,
            attempts: 100);
        started!.AgentRuns.Should().HaveCountGreaterThanOrEqualTo(2);

        var detail = await WaitForFinishedAsync(client, run.Id);
        detail.Run.Status.Should().Be("succeeded");
        detail.AgentRuns.Should().HaveCount(3);
    }

    private static async Task<WorkflowDto> CreateWorkflowAsync(
        HttpClient client, Guid projectId, string name, object[] steps)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/workflows", new
        {
            name,
            description = "集成测试工作流",
            steps,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkflowDto>())!;
    }

    private static async Task<WorkflowRunDto> StartRunAsync(
        HttpClient client, Guid workflowId, Guid workItemId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workflows/{workflowId}/runs", new { workItemId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkflowRunDto>())!;
    }

    private static async Task<WorkflowRunDetailDto> WaitForFinishedAsync(
        HttpClient client, Guid runId)
    {
        var detail = await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<WorkflowRunDetailDto>($"/api/workflow-runs/{runId}"),
            detail => detail is not null &&
                      detail.Run.Status is "succeeded" or "failed" or "canceled",
            attempts: 200);
        return detail!;
    }
}
