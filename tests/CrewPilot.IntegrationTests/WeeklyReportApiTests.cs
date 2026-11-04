using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class WeeklyReportApiTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task WeeklyReport_SummarizesProjectData()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        foreach (var status in new[] { "ready", "inProgress", "inReview", "done" })
        {
            var transition = await client.PostAsJsonAsync(
                $"/api/workitems/{item.Id}/status", new { toStatus = status });
            transition.EnsureSuccessStatusCode();
        }

        await client.PostAsJsonAsync(
            $"/api/workitems/{item.Id}/worklogs", new { hours = 5m, note = "周报测试" });

        var report = await client.GetFromJsonAsync<WeeklyReportDto>(
            $"/api/projects/{project.Id}/weekly-report?days=7");

        report!.CompletedCount.Should().BeGreaterThanOrEqualTo(1);
        report.Markdown.Should().Contain("项目周报");
        report.Markdown.Should().Contain("登记工时：5h");
        report.From.Should().BeBefore(report.To);
    }

    [Fact]
    public async Task WeeklyReportAgent_RequiresManagerAgent()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);

        var missing = await client.PostAsync(
            $"/api/projects/{project.Id}/weekly-report/agent", null);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var manager = await ApiTestHelpers.CreateAgentAsync(
            client, provider.Id, name: "PM Agent", job: "manager");
        manager.Job.Should().Be("manager");

        var created = await client.PostAsync(
            $"/api/projects/{project.Id}/weekly-report/agent", null);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var run = await created.Content.ReadFromJsonAsync<AgentRunListItemDto>();
        run!.Trigger.Should().Be("weekly-report");
        run.Status.Should().Be("queued");
    }
}
