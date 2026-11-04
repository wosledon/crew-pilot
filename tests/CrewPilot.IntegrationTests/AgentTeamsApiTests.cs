using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class AgentTeamsApiTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task DefaultTeam_ResolvesWorkflowAgentByJob()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);
        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var teamResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/agent-teams",
            new { name = "交付小队", goal = "负责需求交付", agentIds = new[] { agent.Id } });
        teamResponse.EnsureSuccessStatusCode();
        var team = await teamResponse.Content.ReadFromJsonAsync<AgentTeamDto>();

        var setDefault = await client.PutAsJsonAsync(
            $"/api/projects/{project.Id}/default-team", new { teamId = team!.Id });
        setDefault.StatusCode.Should().Be(HttpStatusCode.OK);
        (await setDefault.Content.ReadFromJsonAsync<ProjectDetailDto>())!
            .DefaultTeamId.Should().Be(team.Id);

        var workflowResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/workflows",
            new
            {
                name = "单步开发流",
                description = "默认团队解析测试",
                steps = new[]
                {
                    new
                    {
                        name = "实现",
                        order = 0,
                        requiredJob = "developer",
                        approvalPolicy = "auto",
                        maxRetries = 1,
                    },
                },
            });
        workflowResponse.EnsureSuccessStatusCode();
        var workflow = await workflowResponse.Content.ReadFromJsonAsync<WorkflowDto>();

        var start = await client.PostAsJsonAsync(
            $"/api/workflows/{workflow!.Id}/runs", new { workItemId = item.Id });
        start.StatusCode.Should().Be(HttpStatusCode.Created);
        var run = await start.Content.ReadFromJsonAsync<WorkflowRunDto>();

        await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<WorkflowRunDetailDto>(
                $"/api/workflow-runs/{run!.Id}"),
            current => current is not null &&
                       current.Run.Status is "succeeded" or "failed" or "waitingforhuman" or "canceled");

        var detail = await client.GetFromJsonAsync<WorkflowRunDetailDto>(
            $"/api/workflow-runs/{run!.Id}");
        detail!.Run.Status.Should().Be("succeeded");
        detail.AgentRuns.Should().ContainSingle(r => r.AgentName.StartsWith("开发 Agent"));
    }

    [Fact]
    public async Task SetDefaultTeam_WithTeamFromAnotherProject_Returns400()
    {
        var client = factory.CreateClient();
        var first = await ApiTestHelpers.CreateProjectAsync(client);
        var second = await ApiTestHelpers.CreateProjectAsync(client);

        var teamResponse = await client.PostAsJsonAsync(
            $"/api/projects/{first.Id}/agent-teams", new { name = "小队", agentIds = Array.Empty<Guid>() });
        var team = await teamResponse.Content.ReadFromJsonAsync<AgentTeamDto>();

        var response = await client.PutAsJsonAsync(
            $"/api/projects/{second.Id}/default-team", new { teamId = team!.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
