using CrewPilot.Core.Agents;
using CrewPilot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class AgentRunStreamTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task Stream_EmitsStatusLogsAndDone()
    {
        var client = factory.CreateClient();
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);

        Guid runId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrewPilotDbContext>();
            var run = new AgentRun
            {
                AgentId = agent.Id,
                Trigger = "stream-test",
                Prompt = "请执行",
                Output = "完成",
                Status = AgentRunStatus.Succeeded,
                StartedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
                FinishedAt = DateTimeOffset.UtcNow,
                Duration = TimeSpan.FromSeconds(1),
            };
            db.AgentRuns.Add(run);
            await db.SaveChangesAsync();
            db.RunLogs.Add(new RunLog
            {
                AgentRunId = run.Id,
                Sequence = 1,
                Kind = "assistant",
                Content = "第一步",
            });
            db.RunLogs.Add(new RunLog
            {
                AgentRunId = run.Id,
                Sequence = 2,
                Kind = "assistant",
                Content = "第二步",
            });
            await db.SaveChangesAsync();
            runId = run.Id;
        }

        using var response = await client.GetAsync(
            $"/api/agent-runs/{runId}/stream",
            HttpCompletionOption.ResponseHeadersRead);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("event: status");
        body.Should().Contain("\"status\":\"succeeded\"");
        body.Should().Contain("event: log");
        body.Should().Contain("第一步");
        body.Should().Contain("第二步");
        body.Should().Contain("event: done");
    }

    [Fact]
    public async Task Stream_ReturnsNotFoundForUnknownRun()
    {
        var client = factory.CreateClient();
        using var response = await client.GetAsync(
            $"/api/agent-runs/{Guid.NewGuid()}/stream");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}
