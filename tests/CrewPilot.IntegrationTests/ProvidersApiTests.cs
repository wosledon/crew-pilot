using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class ProvidersApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task CreateProvider_ReturnsCreated_WithoutLeakingApiKey()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/providers", new
        {
            name = "测试供应商",
            baseUrl = "https://api.example.com/v1",
            apiKey = "sk-super-secret",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("sk-super-secret");

        var dto = await response.Content.ReadFromJsonAsync<ProviderDetailDto>();
        dto!.BaseUrl.Should().Be("https://api.example.com/v1");
        dto.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProvider_WithDuplicateName_Returns409()
    {
        var client = factory.CreateClient();
        var name = "重复供应商" + Guid.NewGuid().ToString("N")[..6];

        var first = await client.PostAsJsonAsync("/api/providers", new
        {
            name,
            baseUrl = "https://api.example.com/v1",
            apiKey = "sk-1",
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/providers", new
        {
            name,
            baseUrl = "https://api.example.com/v1",
            apiKey = "sk-2",
        });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateProvider_WithInvalidBaseUrl_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/providers", new
        {
            name = "坏地址供应商",
            baseUrl = "not-a-url",
            apiKey = "sk-1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProviders_ListContainsCreated()
    {
        var client = factory.CreateClient();
        var name = "列表供应商" + Guid.NewGuid().ToString("N")[..6];
        await client.PostAsJsonAsync("/api/providers", new
        {
            name,
            baseUrl = "https://api.example.com/v1",
            apiKey = "sk-1",
        });

        var list = await client.GetFromJsonAsync<List<ProviderListItemDto>>("/api/providers");

        list!.Should().Contain(p => p.Name == name);
    }
}
