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
    public async Task Versions_AreSnapshotted_AndRestorable()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var created = await client.PostAsJsonAsync($"/api/projects/{project.Id}/prototypes", new
        {
            name = "版本原型",
            type = "html",
            content = "<p>v1</p>",
        });
        var prototype = await created.Content.ReadFromJsonAsync<PrototypeDto>();

        await client.PutAsJsonAsync($"/api/prototypes/{prototype!.Id}", new
        {
            name = "版本原型 v2",
            content = "<p>v2</p>",
        });

        var versions = await client.GetFromJsonAsync<List<PrototypeVersionDto>>(
            $"/api/prototypes/{prototype.Id}/versions");
        versions.Should().HaveCount(2);
        versions!.OrderByDescending(v => v.Version).First().Content.Should().Be("<p>v2</p>");

        var restore = await client.PostAsync(
            $"/api/prototypes/{prototype.Id}/versions/1/restore", null);
        restore.StatusCode.Should().Be(HttpStatusCode.OK);
        var restored = await restore.Content.ReadFromJsonAsync<PrototypeDto>();
        restored!.Content.Should().Be("<p>v1</p>");
        restored.Version.Should().Be(3);
    }

    [Fact]
    public async Task CreateUrlPrototype_WithNonHttpScheme_Returns400()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/prototypes", new
        {
            name = "危险 URL",
            type = "url",
            content = "javascript:alert(1)",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadImage_AndCreateImagePrototype_ServesBytes()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "shot.png");
        var upload = await client.PostAsync(
            $"/api/projects/{project.Id}/prototype-images", form);
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var image = await upload.Content.ReadFromJsonAsync<PrototypeImageDto>();

        var created = await client.PostAsJsonAsync($"/api/projects/{project.Id}/prototypes", new
        {
            name = "图片原型",
            type = "image",
            content = image!.Path,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var fetched = await client.GetAsync(image.Url);
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        fetched.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await fetched.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
    }

    [Fact]
    public async Task CreatePrototype_WithRequirement_BindsAndFilters()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var requirement = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var created = await client.PostAsJsonAsync($"/api/projects/{project.Id}/prototypes", new
        {
            name = "需求原型",
            type = "html",
            content = "<p>需求关联</p>",
            requirementId = requirement.Id,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var prototype = await created.Content.ReadFromJsonAsync<PrototypeDto>();
        prototype!.RequirementId.Should().Be(requirement.Id);

        var filtered = await client.GetFromJsonAsync<List<PrototypeDto>>(
            $"/api/projects/{project.Id}/prototypes?requirementId={requirement.Id}");
        filtered.Should().ContainSingle(p => p.Id == prototype.Id);
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
