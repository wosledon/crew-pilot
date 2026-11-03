using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class IterationsApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly DateOnly End = new(2026, 11, 14);

    [Fact]
    public async Task CreateIteration_ReturnsCreated()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/iterations", new
        {
            name = "M1 迭代",
            goal = "看板与甘特图",
            startDate = Start,
            endDate = End,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<IterationDetailDto>();
        created!.Status.Should().Be("planned");

        var list = await client.GetFromJsonAsync<List<IterationListItemDto>>(
            $"/api/projects/{project.Id}/iterations");
        list!.Should().ContainSingle(i => i.Name == "M1 迭代");
    }

    [Fact]
    public async Task CreateIteration_WithEndBeforeStart_Returns400()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/iterations", new
        {
            name = "坏迭代",
            startDate = End,
            endDate = Start,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StartAndCompleteIteration_FollowsStateMachine()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var created = await client.PostAsJsonAsync($"/api/projects/{project.Id}/iterations", new
        {
            name = "状态流迭代",
            startDate = Start,
            endDate = End,
        });
        var iteration = await created.Content.ReadFromJsonAsync<IterationDetailDto>();

        // 计划中的迭代不能直接完成
        var earlyComplete = await client.PostAsync($"/api/iterations/{iteration!.Id}/complete", null);
        earlyComplete.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var start = await client.PostAsync($"/api/iterations/{iteration.Id}/start", null);
        start.StatusCode.Should().Be(HttpStatusCode.OK);
        (await start.Content.ReadFromJsonAsync<IterationDetailDto>())!.Status.Should().Be("active");

        var complete = await client.PostAsync($"/api/iterations/{iteration.Id}/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        (await complete.Content.ReadFromJsonAsync<IterationDetailDto>())!.Status.Should().Be("completed");
    }

    [Fact]
    public async Task DeleteIteration_Returns204()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var created = await client.PostAsJsonAsync($"/api/projects/{project.Id}/iterations", new
        {
            name = "待删除迭代",
            startDate = Start,
            endDate = End,
        });
        var iteration = await created.Content.ReadFromJsonAsync<IterationDetailDto>();

        var response = await client.DeleteAsync($"/api/iterations/{iteration!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<ProjectDetailDto> CreateProjectAsync(HttpClient client)
    {
        var key = "I" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var response = await client.PostAsJsonAsync("/api/projects", new { key, name = "迭代测试项目" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDetailDto>())!;
    }
}
