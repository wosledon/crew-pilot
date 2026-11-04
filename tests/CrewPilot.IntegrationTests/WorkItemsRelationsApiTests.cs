using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class WorkItemsRelationsApiTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task Defect_CanLinkAndUnlinkRequirement()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var requirement = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var created = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/workitems", new
            {
                type = "defect",
                title = "关联缺陷",
                relatedRequirementId = requirement.Id,
            });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var defect = await created.Content.ReadFromJsonAsync<WorkItemDetailDto>();
        defect!.RelatedRequirementId.Should().Be(requirement.Id);
        defect.RelatedRequirementTitle.Should().Be(requirement.Title);

        var cleared = await client.PutAsJsonAsync($"/api/workitems/{defect.Id}", new
        {
            title = defect.Title,
            relatedRequirementId = (Guid?)null,
        });
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cleared.Content.ReadFromJsonAsync<WorkItemDetailDto>())!
            .RelatedRequirementId.Should().BeNull();
    }

    [Fact]
    public async Task Defect_WithRequirementFromOtherProject_Returns400()
    {
        var client = factory.CreateClient();
        var first = await ApiTestHelpers.CreateProjectAsync(client);
        var second = await ApiTestHelpers.CreateProjectAsync(client);
        var foreignRequirement = await ApiTestHelpers.CreateRequirementAsync(client, first.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{second.Id}/workitems", new
            {
                type = "defect",
                title = "跨项目缺陷",
                relatedRequirementId = foreignRequirement.Id,
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WorkItems_CanFilterDefectsByRelatedRequirement()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var requirement = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var linked = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/workitems", new
            {
                type = "defect",
                title = "关联缺陷",
                relatedRequirementId = requirement.Id,
            });
        linked.EnsureSuccessStatusCode();
        await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/workitems", new
            {
                type = "defect",
                title = "无关缺陷",
            });

        var defects = await client.GetFromJsonAsync<List<WorkItemListItemDto>>(
            $"/api/projects/{project.Id}/workitems?relatedRequirementId={requirement.Id}");

        defects.Should().ContainSingle();
        defects![0].Title.Should().Be("关联缺陷");
        defects[0].Type.Should().Be("defect");
    }

    [Fact]
    public async Task Activities_CanFilterByWorkItemAndEventType()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var requirement = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var byWorkItem = await client.GetFromJsonAsync<List<ActivityDto>>(
            $"/api/projects/{project.Id}/activities?workItemId={requirement.Id}");
        byWorkItem!.Should().NotBeEmpty();
        byWorkItem.Should().OnlyContain(a => a.WorkItemId == requirement.Id);

        var created = await client.GetFromJsonAsync<List<ActivityDto>>(
            $"/api/projects/{project.Id}/activities?workItemId={requirement.Id}&eventType=workitem.created");
        created.Should().ContainSingle();

        var none = await client.GetFromJsonAsync<List<ActivityDto>>(
            $"/api/projects/{project.Id}/activities?workItemId={requirement.Id}&eventType=workitem.deleted");
        none.Should().BeEmpty();
    }

    [Fact]
    public async Task AgentActivity_ReportsLatestRunPerWorkItem()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);
        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        await client.PostAsJsonAsync($"/api/workitems/{item.Id}/assign-agent", new
        {
            agentId = agent.Id,
        });
        await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<List<AgentRunListItemDto>>(
                $"/api/agent-runs?workItemId={item.Id}"),
            runs => runs is not null && runs.Any(r => r.Status == "succeeded"));

        var activities = await client.GetFromJsonAsync<List<WorkItemAgentActivityDto>>(
            $"/api/projects/{project.Id}/agent-activity");
        var activity = activities!.Single(a => a.WorkItemId == item.Id);
        activity.Status.Should().Be("succeeded");
        activity.AgentName.Should().StartWith("开发 Agent");
    }
}
