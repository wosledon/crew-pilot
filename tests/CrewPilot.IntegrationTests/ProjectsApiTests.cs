using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class ProjectsApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task CreateProject_ReturnsCreated_AndAppearsInList()
    {
        var client = factory.CreateClient();
        var key = NewKey();

        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            key,
            name = "集成测试项目",
            repoUrl = "https://github.com/example/crew-pilot",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        created!.Key.Should().Be(key);
        created.WorkItemCount.Should().Be(0);

        var list = await client.GetFromJsonAsync<List<ProjectSummaryDto>>("/api/projects");
        list!.Should().Contain(p => p.Key == key);
    }

    [Fact]
    public async Task CreateProject_WithDuplicateKey_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var key = NewKey();

        var first = await client.PostAsJsonAsync("/api/projects", new { key, name = "重复 Key 项目" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/projects", new { key, name = "重复 Key 项目" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetProject_WithUnknownId_Returns404()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProject_Returns204()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/projects", new { key = NewKey(), name = "待删除项目" });
        var project = await created.Content.ReadFromJsonAsync<ProjectDetailDto>();

        var response = await client.DeleteAsync($"/api/projects/{project!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var get = await client.GetAsync($"/api/projects/{project.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static string NewKey() => "T" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
}
