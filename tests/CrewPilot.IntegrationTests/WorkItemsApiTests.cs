using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class WorkItemsApiTests(CrewPilotApiFactory factory) : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task CreateRequirement_DefaultsToBacklog_WithHours()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/workitems", new
        {
            type = "requirement",
            title = "搭建项目骨架",
            priority = "p0",
            estimatedHours = 4m,
            assigneeName = "开发 Agent",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<WorkItemDetailDto>();
        item!.Type.Should().Be("requirement");
        item.Status.Should().Be("backlog");
        item.Priority.Should().Be("p0");
        item.EstimatedHours.Should().Be(4m);
        item.LoggedHours.Should().Be(0m);
        item.AssigneeName.Should().Be("开发 Agent");
    }

    [Fact]
    public async Task Transition_BacklogToReadyToInProgress_Succeeds()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var item = await CreateRequirementAsync(client, project.Id);

        var ready = await TransitionAsync(client, item.Id, "ready");
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ready.Content.ReadFromJsonAsync<WorkItemDetailDto>())!.Status.Should().Be("ready");

        var inProgress = await TransitionAsync(client, item.Id, "inProgress");
        inProgress.StatusCode.Should().Be(HttpStatusCode.OK);
        (await inProgress.Content.ReadFromJsonAsync<WorkItemDetailDto>())!.Status.Should().Be("inprogress");
    }

    [Fact]
    public async Task Transition_InProgressToDone_Returns400()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var item = await CreateRequirementAsync(client, project.Id);
        await TransitionAsync(client, item.Id, "ready");

        var response = await TransitionAsync(client, item.Id, "done");

        // inProgress 之前的阶段不允许直接跳到 done（状态机拦截）
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddWorkLog_UpdatesLoggedHours()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var item = await CreateRequirementAsync(client, project.Id);

        var log = await client.PostAsJsonAsync($"/api/workitems/{item.Id}/worklogs", new
        {
            hours = 1.5m,
            note = "领域层",
        });
        log.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await client.GetFromJsonAsync<WorkItemDetailDto>($"/api/workitems/{item.Id}");
        detail!.LoggedHours.Should().Be(1.5m);

        var logs = await client.GetFromJsonAsync<List<WorkLogDto>>($"/api/workitems/{item.Id}/worklogs");
        logs!.Should().ContainSingle().Which.Hours.Should().Be(1.5m);
    }

    [Fact]
    public async Task AddWorkLog_WithZeroHours_Returns400()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var item = await CreateRequirementAsync(client, project.Id);

        var response = await client.PostAsJsonAsync($"/api/workitems/{item.Id}/worklogs", new { hours = 0m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRequirement_WithDueDate_ReturnsDueDate()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        var due = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/workitems", new
        {
            type = "requirement",
            title = "带截止日期的需求",
            dueDate = due,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<WorkItemDetailDto>();
        item!.DueDate.Should().Be(due);
    }

    [Fact]
    public async Task CreateDefect_HasSeverityAndReproSteps()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);

        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/workitems", new
        {
            type = "defect",
            title = "看板拖拽失效",
            severity = "serious",
            reproSteps = "1. 打开看板 2. 拖动卡片",
            environment = "Windows 11",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = await response.Content.ReadFromJsonAsync<WorkItemDetailDto>();
        item!.Type.Should().Be("defect");
        item.Status.Should().Be("new");
        item.Severity.Should().Be("serious");
        item.ReproSteps.Should().Be("1. 打开看板 2. 拖动卡片");
    }

    [Fact]
    public async Task ListWorkItems_FiltersByType()
    {
        var client = factory.CreateClient();
        var project = await CreateProjectAsync(client);
        await CreateRequirementAsync(client, project.Id);
        await client.PostAsJsonAsync($"/api/projects/{project.Id}/workitems", new
        {
            type = "defect",
            title = "缺陷样例",
        });

        var requirements = (await client.GetFromJsonAsync<List<WorkItemListItemDto>>(
            $"/api/projects/{project.Id}/workitems?type=requirement"))!;
        var defects = (await client.GetFromJsonAsync<List<WorkItemListItemDto>>(
            $"/api/projects/{project.Id}/workitems?type=defect"))!;

        requirements.Should().HaveCount(1);
        requirements[0].Type.Should().Be("requirement");
        defects.Should().HaveCount(1);
        defects[0].Type.Should().Be("defect");
    }

    private static async Task<ProjectDetailDto> CreateProjectAsync(HttpClient client)
    {
        var key = "W" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var response = await client.PostAsJsonAsync("/api/projects", new { key, name = "工作项测试项目" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDetailDto>())!;
    }

    private static async Task<WorkItemDetailDto> CreateRequirementAsync(HttpClient client, Guid projectId)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/workitems", new
        {
            type = "requirement",
            title = "测试需求",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkItemDetailDto>())!;
    }

    private static Task<HttpResponseMessage> TransitionAsync(HttpClient client, Guid id, string toStatus) =>
        client.PostAsJsonAsync($"/api/workitems/{id}/status", new { toStatus });
}
