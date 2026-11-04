using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class WorkItemsBatchApiTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task BatchStatus_TransitionsAllValidItems()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var first = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var second = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var response = await client.PostAsJsonAsync("/api/workitems/batch", new
        {
            ids = new[] { first.Id, second.Id, Guid.NewGuid() },
            action = "status",
            toStatus = "ready",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BatchWorkItemsResultDto>();
        result!.Succeeded.Should().Be(2);
        result.Failed.Should().Be(1);

        var items = await client.GetFromJsonAsync<List<WorkItemListItemDto>>(
            $"/api/projects/{project.Id}/workitems");
        items!.Where(i => i.Id == first.Id || i.Id == second.Id)
            .Should().OnlyContain(i => i.Status == "ready");
    }

    [Fact]
    public async Task BatchAssign_AssignsMembers()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var first = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var second = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var response = await client.PostAsJsonAsync("/api/workitems/batch", new
        {
            ids = new[] { first.Id, second.Id },
            action = "assign",
            assigneeName = "批量负责人",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await client.GetFromJsonAsync<List<WorkItemListItemDto>>(
            $"/api/projects/{project.Id}/workitems");
        items!.Where(i => i.Id == first.Id || i.Id == second.Id)
            .Should().OnlyContain(i => i.AssigneeName == "批量负责人");
    }

    [Fact]
    public async Task BatchDelete_RemovesItems()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var first = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var keep = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var response = await client.PostAsJsonAsync("/api/workitems/batch", new
        {
            ids = new[] { first.Id },
            action = "delete",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await client.GetFromJsonAsync<List<WorkItemListItemDto>>(
            $"/api/projects/{project.Id}/workitems");
        items.Should().ContainSingle(i => i.Id == keep.Id);
    }

    [Fact]
    public async Task Batch_WithInvalidPayload_Returns400()
    {
        var client = factory.CreateClient();

        (await (await client.PostAsJsonAsync("/api/workitems/batch", new
        {
            ids = Array.Empty<Guid>(),
            action = "status",
        })).Content.ReadAsStringAsync()).Should().NotBeNullOrEmpty();

        var empty = await client.PostAsJsonAsync("/api/workitems/batch", new
        {
            ids = Array.Empty<Guid>(),
            action = "status",
        });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var invalidAction = await client.PostAsJsonAsync("/api/workitems/batch", new
        {
            ids = new[] { Guid.NewGuid() },
            action = "explode",
        });
        invalidAction.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
