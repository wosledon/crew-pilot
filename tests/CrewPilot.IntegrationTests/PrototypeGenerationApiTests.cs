using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class PrototypeGenerationApiTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task Generate_WritesAgentHtmlIntoPrototype()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client, "原型生成项目");
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id, "产品 Agent", "product");
        factory.Engine.EnqueueOutput(
            "```html\n<!doctype html><html><body><h1>登录</h1></body></html>\n```");

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/prototypes/generate",
            new { brief = "做一个登录页原型" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var generation = await response.Content.ReadFromJsonAsync<PrototypeGenerationDto>();

        generation.Should().NotBeNull();
        var run = await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<AgentRunDetailDto>($"/api/agent-runs/{generation!.RunId}"),
            detail => detail?.Status == "succeeded");
        run.Should().NotBeNull();
        factory.Engine.LastRequest!.Prompt.Should().Contain("做一个登录页原型");

        var prototypes = await client.GetFromJsonAsync<List<PrototypeDto>>(
            $"/api/projects/{project.Id}/prototypes");
        var prototype = prototypes!.Single(p => p.Id == generation!.PrototypeId);
        prototype.Content.Should().Contain("<h1>登录</h1>");
        prototype.Version.Should().Be(2);
        prototype.CreatedByName.Should().Be(agent.Name);
    }
}

/// <summary>独立工厂：确保库里没有产品 Agent，验证缺省提示。</summary>
public sealed class PrototypeGenerationValidationTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task Generate_WithoutProductAgent_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client, "原型无产品 Agent");

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/prototypes/generate",
            new { brief = "做一个登录页" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
