using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class MilestonesApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task CreateListUpdateDelete_Works()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);

        var createdResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/milestones", new
        {
            name = "v1.0",
            description = "首个版本",
            date = "2026-12-31",
        });
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createdResponse.Content.ReadFromJsonAsync<MilestoneDto>();
        created!.Name.Should().Be("v1.0");

        var list = await client.GetFromJsonAsync<List<MilestoneDto>>(
            $"/api/projects/{project.Id}/milestones");
        list.Should().Contain(m => m.Id == created.Id);

        var updatedResponse = await client.PutAsJsonAsync($"/api/milestones/{created.Id}", new
        {
            name = "v1.1",
            description = "调整后的版本",
            date = "2027-01-15",
        });
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<MilestoneDto>();
        updated!.Name.Should().Be("v1.1");

        var deleted = await client.DeleteAsync($"/api/milestones/{created.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        list = await client.GetFromJsonAsync<List<MilestoneDto>>(
            $"/api/projects/{project.Id}/milestones");
        list.Should().NotContain(m => m.Id == created.Id);
    }

    [Fact]
    public async Task IterationCanBeAssignedToMilestone_AndCountIsReported()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);

        var milestoneResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/milestones",
            new { name = "v2.0", date = "2027-03-01" });
        var milestone = await milestoneResponse.Content.ReadFromJsonAsync<MilestoneDto>();

        var iterationResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/iterations",
            new
            {
                name = "Sprint 1",
                startDate = "2027-01-01",
                endDate = "2027-01-14",
                milestoneId = milestone!.Id,
            });
        iterationResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var iteration = await iterationResponse.Content.ReadFromJsonAsync<IterationDetailDto>();
        iteration!.MilestoneId.Should().Be(milestone.Id);

        var milestones = await client.GetFromJsonAsync<List<MilestoneDto>>(
            $"/api/projects/{project.Id}/milestones");
        milestones!.Single(m => m.Id == milestone.Id).IterationCount.Should().Be(1);

        // 删除里程碑后迭代的归属自动置空
        await client.DeleteAsync($"/api/milestones/{milestone.Id}");
        var refreshed = await client.GetFromJsonAsync<IterationDetailDto>(
            $"/api/iterations/{iteration.Id}");
        refreshed!.MilestoneId.Should().BeNull();
    }

    [Fact]
    public async Task IterationWithForeignMilestone_Returns400()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/iterations",
            new
            {
                name = "Sprint X",
                startDate = "2027-01-01",
                endDate = "2027-01-14",
                milestoneId = Guid.NewGuid(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMilestone_WithEmptyName_Returns400()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/milestones",
            new { name = "  ", date = "2026-12-31" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
