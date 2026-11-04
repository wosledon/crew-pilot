using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class DependenciesApiTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task Dependencies_CanBeCreatedListedAndDeleted()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var first = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var second = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var third = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var fs = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/dependencies", new
            {
                fromWorkItemId = first.Id,
                toWorkItemId = second.Id,
                type = "fs",
            });
        fs.StatusCode.Should().Be(HttpStatusCode.Created);
        var fsDto = await fs.Content.ReadFromJsonAsync<WorkItemDependencyDto>();
        fsDto!.Type.Should().Be("finishtostart");
        fsDto.FromTitle.Should().Be(first.Title);

        var ss = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/dependencies", new
            {
                fromWorkItemId = second.Id,
                toWorkItemId = third.Id,
                type = "startToStart",
            });
        ss.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ss.Content.ReadFromJsonAsync<WorkItemDependencyDto>())!
            .Type.Should().Be("starttostart");

        var list = await client.GetFromJsonAsync<List<WorkItemDependencyDto>>(
            $"/api/projects/{project.Id}/dependencies");
        list.Should().HaveCount(2);

        (await client.DeleteAsync($"/api/dependencies/{fsDto.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<List<WorkItemDependencyDto>>(
            $"/api/projects/{project.Id}/dependencies")).Should().HaveCount(1);
    }

    [Fact]
    public async Task CriticalPath_ReturnsLongestWeightedChain()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var a = await CreateRequirementAsync(client, project.Id, "A", 4m);
        var b = await CreateRequirementAsync(client, project.Id, "B", 2m);
        var c = await CreateRequirementAsync(client, project.Id, "C", 6m);
        var d = await CreateRequirementAsync(client, project.Id, "D", 10m);

        foreach (var (from, to) in new[]
                 {
                     (a.Id, b.Id), (b.Id, c.Id), (a.Id, d.Id),
                 })
        {
            var response = await client.PostAsJsonAsync(
                $"/api/projects/{project.Id}/dependencies",
                new { fromWorkItemId = from, toWorkItemId = to, type = "fs" });
            response.EnsureSuccessStatusCode();
        }

        var critical = await client.GetFromJsonAsync<CriticalPathDto>(
            $"/api/projects/{project.Id}/critical-path");

        critical!.TotalHours.Should().Be(14m);
        critical.ItemIds.Should().Equal([a.Id, d.Id]);
    }

    private static async Task<WorkItemDetailDto> CreateRequirementAsync(
        HttpClient client, Guid projectId, string title, decimal estimatedHours)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/workitems",
            new { type = "requirement", title, estimatedHours });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkItemDetailDto>())!;
    }

    [Fact]
    public async Task Dependencies_RejectInvalidShapes()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var other = await ApiTestHelpers.CreateProjectAsync(client);
        var first = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var second = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var third = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var foreign = await ApiTestHelpers.CreateRequirementAsync(client, other.Id);

        var self = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/dependencies",
            new { fromWorkItemId = first.Id, toWorkItemId = first.Id });
        self.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var crossProject = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/dependencies",
            new { fromWorkItemId = first.Id, toWorkItemId = foreign.Id });
        crossProject.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/dependencies",
            new { fromWorkItemId = first.Id, toWorkItemId = second.Id, type = "fs" });
        var duplicate = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/dependencies",
            new { fromWorkItemId = first.Id, toWorkItemId = second.Id, type = "fs" });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/dependencies",
            new { fromWorkItemId = second.Id, toWorkItemId = third.Id, type = "fs" });
        var cycle = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/dependencies",
            new { fromWorkItemId = third.Id, toWorkItemId = first.Id, type = "fs" });
        cycle.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
