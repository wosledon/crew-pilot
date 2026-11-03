using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class AgentsApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task CreateAgent_ReturnsCreated_WithProvider()
    {
        var client = factory.CreateClient();
        var provider = await CreateProviderAsync(client);

        var response = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "开发 Agent",
            job = "developer",
            providerId = provider.Id,
            model = "gpt-4o-mini",
            systemPrompt = "你是一名资深开发者",
            tools = "[\"read_file\",\"write_file\",\"run_command\"]",
            commandWhitelist = "[\"git\",\"dotnet\"]",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<AgentDetailDto>();
        dto!.Job.Should().Be("developer");
        dto.ProviderName.Should().Be(provider.Name);
        dto.Model.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task CreateAgent_WithInvalidJob_Returns400()
    {
        var client = factory.CreateClient();
        var provider = await CreateProviderAsync(client);

        var response = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "坏岗位 Agent",
            job = "wizard",
            providerId = provider.Id,
            model = "gpt-4o-mini",
            systemPrompt = "test",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAgent_WithUnknownProvider_Returns404()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "无供应商 Agent",
            job = "developer",
            providerId = Guid.NewGuid(),
            model = "gpt-4o-mini",
            systemPrompt = "test",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<ProviderDetailDto> CreateProviderAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/providers", new
        {
            name = "Agent 测试供应商" + Guid.NewGuid().ToString("N")[..6],
            baseUrl = "https://api.example.com/v1",
            apiKey = "sk-test",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProviderDetailDto>())!;
    }
}
