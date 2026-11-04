using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Infrastructure.Cicd;
using CrewPilot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class IntegrationsApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task UpsertIntegration_HidesToken_AndTestsConnection()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);

        var upsert = await client.PutAsJsonAsync($"/api/projects/{project.Id}/integration", new
        {
            platform = "github",
            token = "secret-token-123",
            repository = "example/crew-pilot",
        });
        upsert.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await upsert.Content.ReadAsStringAsync();
        raw.Should().NotContain("secret-token-123");

        var integration = await upsert.Content.ReadFromJsonAsync<ProjectIntegrationDto>();
        integration!.Platform.Should().Be("github");
        integration.HasToken.Should().BeTrue();
        integration.Repository.Should().Be("example/crew-pilot");

        var test = await client.PostAsync(
            $"/api/projects/{project.Id}/integration/test", null);
        test.StatusCode.Should().Be(HttpStatusCode.OK);
        (await test.Content.ReadFromJsonAsync<TestIntegrationResponse>())!.Ok.Should().BeTrue();

        // 不带 token 的更新保留原 token
        var update = await client.PutAsJsonAsync($"/api/projects/{project.Id}/integration", new
        {
            platform = "gitlab",
        });
        (await update.Content.ReadFromJsonAsync<ProjectIntegrationDto>())!
            .HasToken.Should().BeTrue();

        (await client.DeleteAsync($"/api/projects/{project.Id}/integration"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/projects/{project.Id}/integration"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PullRequestCreateAndSync_UpdatesWorkItemAndMergesToDone()
    {
        var client = factory.CreateClient();
        var directory = Directory.CreateTempSubdirectory("crewpilot_cicd_");
        try
        {
            var project = await CreateProjectWithLocalPathAsync(client, directory.FullName);
            await ConfigureIntegrationAsync(client, project.Id);
            var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
            await TransitionAsync(client, item.Id, "ready");
            await TransitionAsync(client, item.Id, "inProgress");
            await TransitionAsync(client, item.Id, "inReview");

            var create = await client.PostAsJsonAsync(
                $"/api/workitems/{item.Id}/pull-request",
                new { branch = "feat/CP-1-demo", title = "实现示例需求", body = "说明" });
            create.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await create.Content.ReadFromJsonAsync<PullRequestCreatedDto>();
            created!.Url.Should().Be(factory.GitCli.PullRequestUrl);

            var detail = await client.GetFromJsonAsync<WorkItemDetailDto>($"/api/workitems/{item.Id}");
            detail!.PrUrl.Should().Be(factory.GitCli.PullRequestUrl);

            factory.GitCli.Status = new PullRequestStatus(
                "MERGED", true, [new CiCheck("build", "completed", "success", null)],
                factory.GitCli.PullRequestUrl);

            var sync = await client.PostAsync(
                $"/api/workitems/{item.Id}/pull-request/sync", null);
            sync.StatusCode.Should().Be(HttpStatusCode.OK);
            var status = await sync.Content.ReadFromJsonAsync<PullRequestStatusDto>();
            status!.State.Should().Be("merged");
            status.CiStatus.Should().Be("success");

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CrewPilotDbContext>();
            var stored = await db.WorkItems.AsNoTracking().FirstAsync(w => w.Id == item.Id);
            stored.PrState.Should().Be("merged");
            stored.CiStatus.Should().Be("success");

            // PR 合并后需求自动流转到 Done
            var after = await client.GetFromJsonAsync<WorkItemDetailDto>($"/api/workitems/{item.Id}");
            after!.Status.Should().Be("done");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task CreateRelease_UsesCliAndRecordsActivity()
    {
        var client = factory.CreateClient();
        var directory = Directory.CreateTempSubdirectory("crewpilot_cicd_");
        try
        {
            var project = await CreateProjectWithLocalPathAsync(client, directory.FullName);
            await ConfigureIntegrationAsync(client, project.Id);
            var before = factory.GitCli.CreateReleaseCalls;

            var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/releases", new
            {
                tag = "v1.0.0",
                title = "1.0.0",
                notes = "首个版本",
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadFromJsonAsync<ReleaseDto>())!.Tag.Should().Be("v1.0.0");
            factory.GitCli.CreateReleaseCalls.Should().Be(before + 1);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PullRequestSync_WithoutIntegration_Returns404()
    {
        var client = factory.CreateClient();
        var directory = Directory.CreateTempSubdirectory("crewpilot_cicd_");
        try
        {
            var project = await CreateProjectWithLocalPathAsync(client, directory.FullName);
            var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

            var response = await client.PostAsync(
                $"/api/workitems/{item.Id}/pull-request/sync", null);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<ProjectDetailDto> CreateProjectWithLocalPathAsync(
        HttpClient client, string localPath)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            key = "I" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            name = "CI/CD 测试项目",
            localPath,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDetailDto>())!;
    }

    private static async Task ConfigureIntegrationAsync(HttpClient client, Guid projectId)
    {
        var response = await client.PutAsJsonAsync($"/api/projects/{projectId}/integration", new
        {
            platform = "github",
            token = "ghp_test_token",
        });
        response.EnsureSuccessStatusCode();
    }

    private static async Task TransitionAsync(HttpClient client, Guid itemId, string toStatus)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/workitems/{itemId}/status", new { toStatus });
        response.EnsureSuccessStatusCode();
    }
}
