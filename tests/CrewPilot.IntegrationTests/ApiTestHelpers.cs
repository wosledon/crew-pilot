using System.Net.Http.Json;
using CrewPilot.Api.Contracts;

namespace CrewPilot.IntegrationTests;

/// <summary>集成测试通用数据构造：项目 / 工作项 / 供应商 / Agent。</summary>
internal static class ApiTestHelpers
{
    public static async Task<ProjectDetailDto> CreateProjectAsync(
        HttpClient client, string name = "测试项目")
    {
        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            key = "T" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            name,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDetailDto>())!;
    }

    public static async Task<WorkItemDetailDto> CreateRequirementAsync(
        HttpClient client, Guid projectId, string title = "测试需求")
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/workitems", new
        {
            type = "requirement",
            title,
            description = "集成测试用需求描述",
            acceptanceCriteria = "- [ ] 完成",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkItemDetailDto>())!;
    }

    public static async Task<ProviderDetailDto> CreateProviderAsync(
        HttpClient client, string name = "测试供应商")
    {
        var response = await client.PostAsJsonAsync("/api/providers", new
        {
            name = name + "-" + Guid.NewGuid().ToString("N")[..6],
            baseUrl = "https://api.example.com/v1",
            apiKey = "test-key",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProviderDetailDto>())!;
    }

    public static async Task<AgentDetailDto> CreateAgentAsync(
        HttpClient client, Guid providerId, string name = "开发 Agent")
    {
        var response = await client.PostAsJsonAsync("/api/agents", new
        {
            name = name + "-" + Guid.NewGuid().ToString("N")[..6],
            job = "developer",
            providerId,
            model = "test-model",
            systemPrompt = "你是开发 Agent",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentDetailDto>())!;
    }

    public static async Task<T> WaitForAsync<T>(
        Func<Task<T>> action, Func<T, bool> predicate, int attempts = 100)
    {
        for (var i = 0; i < attempts; i++)
        {
            var value = await action();
            if (predicate(value))
            {
                return value;
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException("等待条件超时");
    }
}
