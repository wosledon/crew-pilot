using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class ReportsApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task HoursReport_AggregatesByTypeAndAssignee()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);

        var requirement = await CreateWorkItemAsync(client, project.Id, new
        {
            type = "requirement",
            title = "报表需求",
            estimatedHours = 6m,
            assigneeName = "开发 Agent",
        });
        await CreateWorkItemAsync(client, project.Id, new
        {
            type = "defect",
            title = "报表缺陷",
            estimatedHours = 2m,
        });
        await client.PostAsJsonAsync($"/api/workitems/{requirement.Id}/worklogs", new { hours = 1.5m });

        var report = await client.GetFromJsonAsync<HoursReportDto>(
            $"/api/projects/{project.Id}/hours-report");

        report!.TotalEstimatedHours.Should().Be(8m);
        report.TotalLoggedHours.Should().Be(1.5m);
        report.ByType.Should().HaveCount(2);
        report.ByType.Should().Contain(g => g.Key == "requirement" && g.EstimatedHours == 6m);
        report.ByAssignee.Should().Contain(g => g.Key == "开发 Agent" && g.LoggedHours == 1.5m);
        report.ByMember.Should().Contain(g => g.Key == "我" && g.LoggedHours == 1.5m);
    }

    [Fact]
    public async Task Burndown_ReturnsDailyPoints()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var iteration = await client.PostAsJsonAsync($"/api/projects/{project.Id}/iterations", new
        {
            name = "燃尽迭代",
            startDate = today.AddDays(-2),
            endDate = today.AddDays(5),
        });
        var iterationDto = await iteration.Content.ReadFromJsonAsync<IterationDetailDto>();

        var item = await CreateWorkItemAsync(client, project.Id, new
        {
            type = "requirement",
            title = "燃尽需求",
            estimatedHours = 4m,
            iterationId = iterationDto!.Id,
        });
        await client.PostAsJsonAsync($"/api/workitems/{item.Id}/worklogs", new { hours = 1m });

        var burndown = await client.GetFromJsonAsync<BurndownDto>(
            $"/api/iterations/{iterationDto!.Id}/burndown");

        burndown!.TotalEstimatedHours.Should().Be(4m);
        burndown.Points.Should().NotBeEmpty();
        burndown.Points.Last().RemainingHours.Should().Be(3m);
    }

    private static async Task<ProjectDetailDto> CreateProjectAsync(HttpClient client)
    {
        var key = "R" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var response = await client.PostAsJsonAsync("/api/projects", new { key, name = "报表测试项目" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDetailDto>())!;
    }

    private static async Task<WorkItemDetailDto> CreateWorkItemAsync(
        HttpClient client, Guid projectId, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/workitems", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkItemDetailDto>())!;
    }
}
