using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class PrototypesApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task CreateHtmlPrototype_AndGet()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/prototypes", new
        {
            name = "首页卡片",
            type = "html",
            content = "<h1>hello crew</h1>",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<PrototypeDto>();
        created!.Type.Should().Be("html");
        created.Version.Should().Be(1);
        created.Content.Should().Be("<h1>hello crew</h1>");

        var fetched = await client.GetFromJsonAsync<PrototypeDto>($"/api/prototypes/{created.Id}");
        fetched!.Name.Should().Be("首页卡片");
    }

    [Fact]
    public async Task UpdatePrototype_IncrementsVersion()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var created = await client.PostAsJsonAsync($"/api/projects/{project.Id}/prototypes", new
        {
            name = "原型 v1",
            type = "html",
            content = "<p>v1</p>",
        });
        var prototype = await created.Content.ReadFromJsonAsync<PrototypeDto>();

        var updated = await client.PutAsJsonAsync($"/api/prototypes/{prototype!.Id}", new
        {
            name = "原型 v2",
            content = "<p>v2</p>",
        });

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await updated.Content.ReadFromJsonAsync<PrototypeDto>();
        dto!.Version.Should().Be(2);
        dto.Content.Should().Be("<p>v2</p>");
    }

    [Fact]
    public async Task CreatePrototype_WithInvalidType_Returns400()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/prototypes", new
        {
            name = "坏原型",
            type = "figma-native",
            content = "x",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<ProjectDetailDto> CreateProjectAsync(HttpClient client)
    {
        var key = "P" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var response = await client.PostAsJsonAsync("/api/projects", new { key, name = "原型测试项目" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDetailDto>())!;
    }
}
