using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class DashboardApiTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task Dashboard_AggregatesProjectsAndOwnerTodos()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client, "驾驶舱项目");
        var requirement = await CreateOwnedRequirementAsync(client, project.Id, "需要我拍板的需求");

        var dashboard = await client.GetFromJsonAsync<DashboardDto>("/api/dashboard");

        dashboard.Should().NotBeNull();
        dashboard!.Stats.ProjectCount.Should().BeGreaterThanOrEqualTo(1);
        dashboard.Projects.Should().Contain(p => p.Id == project.Id && p.WorkItemCount >= 1);
        dashboard.Todos.Should().Contain(t => t.WorkItemId == requirement.Id);
        dashboard.Todos.Should().OnlyContain(t =>
            t.Type == "requirement" || t.Type == "defect" || t.Type == "task");
    }

    [Fact]
    public async Task AssigningToAgentName_RegistersAgentMember()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client, "Agent 成员项目");
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id, "成员映射 Agent");

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/workitems", new
        {
            type = "requirement",
            title = "交给数字员工",
            assigneeName = agent.Name,
        });
        response.EnsureSuccessStatusCode();

        var members = await client.GetFromJsonAsync<List<ProjectMemberDto>>(
            $"/api/projects/{project.Id}/members");
        members.Should().NotBeNull();
        members!.Should().Contain(m =>
            m.DisplayName == agent.Name && m.Kind == "agent" && m.Role == "developer");
    }

    private static async Task<WorkItemDetailDto> CreateOwnedRequirementAsync(
        HttpClient client, Guid projectId, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/workitems", new
        {
            type = "requirement",
            title,
            assigneeName = "我",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<WorkItemDetailDto>())!;
    }
}
