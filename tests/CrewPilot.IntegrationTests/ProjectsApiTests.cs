using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class ProjectsApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task CreateProject_ReturnsCreated_AndAppearsInList()
    {
        var client = factory.CreateClient();
        var key = NewKey();

        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            key,
            name = "集成测试项目",
            repoUrl = "https://github.com/example/crew-pilot",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
        created!.Key.Should().Be(key);
        created.WorkItemCount.Should().Be(0);

        var list = await client.GetFromJsonAsync<List<ProjectSummaryDto>>("/api/projects");
        list!.Should().Contain(p => p.Key == key);
    }

    [Fact]
    public async Task CreateProject_WithDuplicateKey_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var key = NewKey();

        var first = await client.PostAsJsonAsync("/api/projects", new { key, name = "重复 Key 项目" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/projects", new { key, name = "重复 Key 项目" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetProject_WithUnknownId_Returns404()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProject_Returns204()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/projects", new { key = NewKey(), name = "待删除项目" });
        var project = await created.Content.ReadFromJsonAsync<ProjectDetailDto>();

        var response = await client.DeleteAsync($"/api/projects/{project!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var get = await client.GetAsync($"/api/projects/{project.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateProject_WithExistingLocalPath_ReturnsNormalizedPath()
    {
        var client = factory.CreateClient();
        var directory = Directory.CreateTempSubdirectory("crewpilot_project_");

        try
        {
            var response = await client.PostAsJsonAsync("/api/projects", new
            {
                key = NewKey(),
                name = "带本地目录的项目",
                localPath = directory.FullName,
            });

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await response.Content.ReadFromJsonAsync<ProjectDetailDto>();
            created!.LocalPath.Should().Be(Path.GetFullPath(directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task CreateProject_WithMissingLocalPath_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            key = NewKey(),
            name = "本地目录不存在",
            localPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProject_WithEmptyLocalPath_ClearsIt()
    {
        var client = factory.CreateClient();
        var directory = Directory.CreateTempSubdirectory("crewpilot_project_");

        try
        {
            var createdResponse = await client.PostAsJsonAsync("/api/projects", new
            {
                key = NewKey(),
                name = "待清空目录",
                localPath = directory.FullName,
            });
            var created = await createdResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();

            var updatedResponse = await client.PutAsJsonAsync($"/api/projects/{created!.Id}", new
            {
                name = "待清空目录",
                localPath = string.Empty,
            });

            updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var updated = await updatedResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();
            updated!.LocalPath.Should().BeNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task UpdateProject_ChangesFields_AndArchiveTogglesStatus()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/projects", new
        {
            key = NewKey(),
            name = "待归档项目",
        });
        var project = await created.Content.ReadFromJsonAsync<ProjectDetailDto>();

        var updatedResponse = await client.PutAsJsonAsync($"/api/projects/{project!.Id}", new
        {
            name = "已改名项目",
            description = "新的描述",
            repoUrl = "https://github.com/example/renamed",
        });
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<ProjectDetailDto>();
        updated!.Name.Should().Be("已改名项目");
        updated.Description.Should().Be("新的描述");
        updated.Status.Should().Be("active");

        var archivedResponse = await client.PostAsync($"/api/projects/{project.Id}/archive", null);
        archivedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await archivedResponse.Content.ReadFromJsonAsync<ProjectDetailDto>())!
            .Status.Should().Be("archived");

        var unarchivedResponse = await client.PostAsync($"/api/projects/{project.Id}/unarchive", null);
        unarchivedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await unarchivedResponse.Content.ReadFromJsonAsync<ProjectDetailDto>())!
            .Status.Should().Be("active");
    }

    [Fact]
    public async Task ProjectMembers_CanAddAgentUpdateAndRemove()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);

        var addResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project.Id}/members", new { agentId = agent.Id });
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var member = await addResponse.Content.ReadFromJsonAsync<ProjectMemberDto>();
        member!.Kind.Should().Be("agent");
        member.Role.Should().Be("developer");
        member.DisplayName.Should().Be(agent.Name);

        var members = await client.GetFromJsonAsync<List<ProjectMemberDto>>(
            $"/api/projects/{project.Id}/members");
        members.Should().Contain(m => m.Id == member.Id);
        members!.Should().Contain(m => m.Role == "owner");

        var updateResponse = await client.PutAsJsonAsync($"/api/members/{member.Id}", new
        {
            displayName = agent.Name,
            role = "qa",
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updateResponse.Content.ReadFromJsonAsync<ProjectMemberDto>())!
            .Role.Should().Be("qa");

        var deleteResponse = await client.DeleteAsync($"/api/members/{member.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        members = await client.GetFromJsonAsync<List<ProjectMemberDto>>(
            $"/api/projects/{project.Id}/members");
        members.Should().NotContain(m => m.Id == member.Id);
    }

    [Fact]
    public async Task CannotRemoveOwnerMember()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var members = await client.GetFromJsonAsync<List<ProjectMemberDto>>(
            $"/api/projects/{project.Id}/members");
        var owner = members!.Single(m => m.Role == "owner");

        var response = await client.DeleteAsync($"/api/members/{owner.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string NewKey() => "T" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
}
