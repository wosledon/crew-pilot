using System.Net;
using System.Net.Http.Json;
using CrewPilot.Api.Contracts;
using FluentAssertions;
using Xunit;

namespace CrewPilot.IntegrationTests;

public sealed class PlatformExtrasApiTests(CrewPilotApiFactory factory)
    : IClassFixture<CrewPilotApiFactory>
{
    [Fact]
    public async Task SkillCrud_Works()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/skills", new
        {
            name = "PRD 撰写",
            description = "把想法整理成结构化需求",
            promptContent = "请输出用户故事与验收标准。",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var skill = await created.Content.ReadFromJsonAsync<SkillDto>();
        skill!.Name.Should().Be("PRD 撰写");

        var list = await client.GetFromJsonAsync<List<SkillDto>>("/api/skills");
        list.Should().Contain(s => s.Id == skill.Id);

        var updated = await client.PutAsJsonAsync($"/api/skills/{skill.Id}", new
        {
            name = "PRD 撰写 v2",
            description = "更新后的说明",
            promptContent = "输出用户故事、验收标准与风险。",
            tools = "read_file",
            enabled = true,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<SkillDto>())!.Name.Should().Be("PRD 撰写 v2");

        (await client.DeleteAsync($"/api/skills/{skill.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AgentBinding_InjectsSkillIntoSystemPrompt()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);
        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var skillResponse = await client.PostAsJsonAsync("/api/skills", new
        {
            name = "测试技能",
            promptContent = "技能注入标记：SKILL_PROMPT_MARKER",
        });
        var skill = await skillResponse.Content.ReadFromJsonAsync<SkillDto>();

        var update = await client.PutAsJsonAsync($"/api/agents/{agent.Id}", new
        {
            name = agent.Name,
            job = agent.Job,
            systemPrompt = agent.SystemPrompt,
            tools = agent.Tools,
            commandWhitelist = agent.CommandWhitelist,
            enabled = true,
            skillIds = new[] { skill!.Id },
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<AgentDetailDto>())!
            .SkillIds.Should().Contain(skill.Id);

        var assign = await client.PostAsJsonAsync(
            $"/api/workitems/{item.Id}/assign-agent", new { agentId = agent.Id });
        assign.StatusCode.Should().Be(HttpStatusCode.Created);

        await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<List<AgentRunListItemDto>>(
                $"/api/agent-runs?workItemId={item.Id}"),
            runs => runs is not null && runs.Any(r => r.Status == "succeeded"));

        factory.Engine.LastRequest.Should().NotBeNull();
        factory.Engine.LastRequest!.SystemPrompt.Should().Contain("SKILL_PROMPT_MARKER");
    }

    [Fact]
    public async Task McpServer_Crud_HidesToken_AndTestsEndpoint()
    {
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/mcp-servers", new
        {
            name = "设计 MCP",
            endpoint = "http://127.0.0.1:9/mcp",
            transport = "http",
            authToken = "mcp-secret-token",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var raw = await created.Content.ReadAsStringAsync();
        raw.Should().NotContain("mcp-secret-token");
        var server = await created.Content.ReadFromJsonAsync<McpServerDto>();
        server!.HasToken.Should().BeTrue();
        server.Transport.Should().Be("http");

        var test = await client.PostAsync($"/api/mcp-servers/{server.Id}/test", null);
        test.StatusCode.Should().Be(HttpStatusCode.OK);
        (await test.Content.ReadFromJsonAsync<McpTestDto>())!.Ok.Should().BeFalse();

        (await client.DeleteAsync($"/api/mcp-servers/{server.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var audit = await client.GetFromJsonAsync<List<AuditLogDto>>("/api/audit?take=200");
        audit.Should().Contain(a => a.Action == "mcp.delete");
    }

    [Fact]
    public async Task AgentBinding_WithUnknownMcpServer_Returns400()
    {
        var client = factory.CreateClient();
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);

        var update = await client.PutAsJsonAsync($"/api/agents/{agent.Id}", new
        {
            name = agent.Name,
            job = agent.Job,
            systemPrompt = agent.SystemPrompt,
            enabled = true,
            mcpServerIds = new[] { Guid.NewGuid() },
        });

        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProviderModelCrud_AndCostReport()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);

        var modelResponse = await client.PostAsJsonAsync($"/api/providers/{provider.Id}/models", new
        {
            modelId = "cost-model",
            displayName = "成本模型",
            inputPricePerMillion = 2.0m,
            outputPricePerMillion = 4.0m,
        });
        modelResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var model = await modelResponse.Content.ReadFromJsonAsync<ModelDto>();

        var duplicate = await client.PostAsJsonAsync($"/api/providers/{provider.Id}/models", new
        {
            modelId = "cost-model",
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var update = await client.PutAsJsonAsync($"/api/providers/models/{model!.Id}", new
        {
            modelId = "cost-model",
            displayName = "成本模型 v2",
            inputPricePerMillion = 2.0m,
            outputPricePerMillion = 4.0m,
            enabled = true,
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var agentResponse = await client.PostAsJsonAsync("/api/agents", new
        {
            name = "成本Agent-" + Guid.NewGuid().ToString("N")[..6],
            job = "developer",
            providerId = provider.Id,
            model = "cost-model",
            systemPrompt = "测试",
        });
        agentResponse.EnsureSuccessStatusCode();
        var agent = await agentResponse.Content.ReadFromJsonAsync<AgentDetailDto>();

        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);
        await client.PostAsJsonAsync($"/api/workitems/{item.Id}/assign-agent", new
        {
            agentId = agent!.Id,
        });

        await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<List<AgentRunListItemDto>>(
                $"/api/agent-runs?workItemId={item.Id}"),
            runs => runs is not null && runs.Any(r => r.Status == "succeeded"));

        var report = await client.GetFromJsonAsync<CostReportDto>(
            $"/api/projects/{project.Id}/cost-report");
        report!.TotalPromptTokens.Should().Be(1200);
        report.TotalCompletionTokens.Should().Be(300);
        report.TotalCost.Should().Be(0.0036m);
        report.ByModel.Should().ContainSingle();

        (await client.DeleteAsync($"/api/providers/models/{model.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SetDefaultProvider_SwitchesSingleDefault()
    {
        var client = factory.CreateClient();
        var first = await ApiTestHelpers.CreateProviderAsync(client);
        var second = await ApiTestHelpers.CreateProviderAsync(client);

        var response = await client.PutAsync($"/api/providers/{second.Id}/default", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var providers = await client.GetFromJsonAsync<List<ProviderListItemDto>>(
            "/api/providers");
        providers!.Where(p => p.IsDefault).Should().ContainSingle()
            .Which.Id.Should().Be(second.Id);
        providers!.Single(p => p.Id == first.Id).IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task SystemPromptTemplate_IsRenderedPerWorkItem()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);
        var provider = await ApiTestHelpers.CreateProviderAsync(client);
        var agent = await ApiTestHelpers.CreateAgentAsync(client, provider.Id);
        var item = await ApiTestHelpers.CreateRequirementAsync(client, project.Id);

        var skill = await (await client.PostAsJsonAsync("/api/skills", new
        {
            name = "模板技能",
            promptContent = "技能要求：{{title}}",
        })).Content.ReadFromJsonAsync<SkillDto>();

        var update = await client.PutAsJsonAsync($"/api/agents/{agent.Id}", new
        {
            name = agent.Name,
            job = agent.Job,
            systemPrompt = "需求标题：{{title}}；类型：{{type}}；状态：{{status}}",
            enabled = true,
            skillIds = new[] { skill!.Id },
        });
        update.EnsureSuccessStatusCode();

        await client.PostAsJsonAsync($"/api/workitems/{item.Id}/assign-agent", new
        {
            agentId = agent.Id,
        });
        await ApiTestHelpers.WaitForAsync(
            () => client.GetFromJsonAsync<List<AgentRunListItemDto>>(
                $"/api/agent-runs?workItemId={item.Id}"),
            runs => runs is not null && runs.Any(r => r.Status == "succeeded"));

        var systemPrompt = factory.Engine.LastRequest!.SystemPrompt;
        systemPrompt.Should().Contain($"需求标题：{item.Title}");
        systemPrompt.Should().Contain("类型：requirement");
        systemPrompt.Should().Contain("状态：Backlog");
        systemPrompt.Should().Contain($"技能要求：{item.Title}");
    }

    [Fact]
    public async Task Audit_RecordsSensitiveDeletes()
    {
        var client = factory.CreateClient();
        var project = await ApiTestHelpers.CreateProjectAsync(client);

        (await client.DeleteAsync($"/api/projects/{project.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var audit = await client.GetFromJsonAsync<List<AuditLogDto>>(
            $"/api/audit?projectId={project.Id}");
        audit.Should().Contain(a => a.Action == "project.delete");
    }
}
