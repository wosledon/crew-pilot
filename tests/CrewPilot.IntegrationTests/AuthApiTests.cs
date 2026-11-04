using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class AuthApiTests : IDisposable
{
    private readonly AuthEnabledApiFactory _factory = new();

    [Fact]
    public async Task AuthDisabledByDefault_StatusReportsDisabled()
    {
        using var defaultFactory = new CrewPilotApiFactory();
        var client = defaultFactory.CreateClient();

        var status = await client.GetFromJsonAsync<AuthStatusDto>("/api/auth/status");
        status!.Enabled.Should().BeFalse();
        status.Authenticated.Should().BeFalse();
    }

    [Fact]
    public async Task ProtectedApi_RequiresLogin_AndCookieWorks()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });

        (await client.GetAsync("/api/projects")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);

        var status = await client.GetFromJsonAsync<AuthStatusDto>("/api/auth/status");
        status!.Enabled.Should().BeTrue();
        status.Authenticated.Should().BeFalse();

        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { password = "wrong" });
        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new { password = "test-password" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync("/api/projects")).StatusCode.Should().Be(HttpStatusCode.OK);

        var authenticated = await client.GetFromJsonAsync<AuthStatusDto>("/api/auth/status");
        authenticated!.Authenticated.Should().BeTrue();

        (await client.PostAsync("/api/auth/logout", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/projects")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    public void Dispose() => _factory.Dispose();
}
