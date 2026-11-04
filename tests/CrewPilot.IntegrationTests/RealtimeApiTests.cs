using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class RealtimeApiTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task RealtimeHub_Negotiate_ReturnsConnection()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/hubs/realtime/negotiate?negotiateVersion=1", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var negotiation = await response.Content.ReadFromJsonAsync<Negotiation>();
        negotiation!.ConnectionId.Should().NotBeNullOrWhiteSpace();
        negotiation.AvailableTransports.Should().NotBeEmpty();
    }

    private sealed record Negotiation(
        string ConnectionId,
        IReadOnlyList<System.Text.Json.JsonElement> AvailableTransports);
}
