using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class InfrastructureApiTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task Health_ReturnsOkWithDatabaseProvider()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Correlation-Id").Should().NotBeEmpty();
        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();
        payload!.Status.Should().Be("ok");
        payload.Database.Should().Be("Sqlite");
    }

    private sealed record HealthPayload(string Status, string Database);
}
