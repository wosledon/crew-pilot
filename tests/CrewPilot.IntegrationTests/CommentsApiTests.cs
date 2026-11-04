using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class CommentsApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task CreateComment_AppearsInThread_AndCanBeDeleted()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var workItem = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var created = await client.PostAsJsonAsync(
            $"/api/workitems/{workItem.Id}/comments",
            new { content = "这是一条测试评论" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await created.Content.ReadFromJsonAsync<CreateCommentResultDto>();
        result!.Comment.Content.Should().Be("这是一条测试评论");
        result.Comment.AuthorKind.Should().Be("user");
        result.AgentRunId.Should().BeNull();

        var comments = await client.GetFromJsonAsync<List<CommentDto>>(
            $"/api/workitems/{workItem.Id}/comments");
        comments.Should().Contain(c => c.Id == result.Comment.Id);

        var deleted = await client.DeleteAsync($"/api/comments/{result.Comment.Id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        comments = await client.GetFromJsonAsync<List<CommentDto>>(
            $"/api/workitems/{workItem.Id}/comments");
        comments.Should().NotContain(c => c.Id == result.Comment.Id);
    }

    [Fact]
    public async Task CreateComment_WithEmptyContent_Returns400()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var workItem = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/workitems/{workItem.Id}/comments",
            new { content = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CommentMentioningAgent_TriggersRun_AndAgentRepliesInThread()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var workItem = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);

        var created = await client.PostAsJsonAsync(
            $"/api/workitems/{workItem.Id}/comments",
            new { content = "@开发 Agent 请给出实现方案", mentionAgentId = agent.Id });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await created.Content.ReadFromJsonAsync<CreateCommentResultDto>();
        result!.AgentRunId.Should().NotBeNull();

        var run = await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<AgentRunDetailDto>($"/api/agent-runs/{result.AgentRunId}"),
            detail => detail is not null && detail.Status is "succeeded" or "failed");
        run!.Status.Should().Be("succeeded");

        var comments = await ApiTestHelpers.WaitForAsync(
            async () =>
                (await client.GetFromJsonAsync<List<CommentDto>>(
                    $"/api/workitems/{workItem.Id}/comments"))!,
            list => list.Any(c => c.AuthorKind == "agent"));

        var reply = comments.Single(c => c.AuthorKind == "agent");
        reply.AuthorName.Should().Be(agent.Name);
        reply.Content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateComment_WithUnknownAgent_Returns400()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var workItem = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/workitems/{workItem.Id}/comments",
            new { content = "测试", mentionAgentId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
