using CrewPilot.Api.Contracts;
using CrewPilot.Core.Agents;
using CrewPilot.Core.Mcp;
using CrewPilot.Core.Skills;
using CrewPilot.Infrastructure.Audit;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class AgentsController(
    CrewPilotDbContext db,
    AuditLogger audit) : ControllerBase
{
    [HttpGet("api/agents")]
    public async Task<ActionResult<IReadOnlyList<AgentListItemDto>>> List(CancellationToken ct)
    {
        var rows = await db.Agents
            .AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Job,
                ProviderName = a.Provider.Name,
                a.Model,
                a.Enabled,
                RunCount = a.Runs.Count,
            })
            .ToListAsync(ct);

        var items = rows
            .OrderBy(a => a.Name)
            .Select(a => new AgentListItemDto(
                a.Id, a.Name, a.Job.ToString().ToLowerInvariant(),
                a.ProviderName, a.Model, a.Enabled, a.RunCount))
            .ToList();
        return Ok(items);
    }

    [HttpGet("api/agents/{id:guid}")]
    public async Task<ActionResult<AgentDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var agent = await LoadAgentAsync(id, ct);
        return agent is null
            ? Problem(detail: "Agent 不存在", statusCode: 404)
            : Ok(ToDto(agent));
    }

    [HttpPost("api/agents")]
    public async Task<ActionResult<AgentDetailDto>> Create(
        [FromBody] CreateAgentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "Agent 名称必填", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return Problem(detail: "模型必填", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            return Problem(detail: "系统提示词必填", statusCode: 400);
        }

        if (!Enum.TryParse<AgentJob>(request.Job, ignoreCase: true, out var job))
        {
            return Problem(detail: $"无效的岗位 {request.Job}", statusCode: 400);
        }

        var provider = await db.ModelProviders.FirstOrDefaultAsync(p => p.Id == request.ProviderId, ct);
        if (provider is null)
        {
            return Problem(detail: "供应商不存在", statusCode: 404);
        }

        var bindingError = await ValidateBindingsAsync(
            request.SkillIds, request.McpServerIds, ct);
        if (bindingError is not null)
        {
            return Problem(detail: bindingError, statusCode: 400);
        }

        var agent = new Agent
        {
            Name = request.Name.Trim(),
            Job = job,
            ProviderId = provider.Id,
            Model = request.Model.Trim(),
            SystemPrompt = request.SystemPrompt,
            Tools = request.Tools,
            CommandWhitelist = request.CommandWhitelist,
            SandboxPolicy = ParseSandboxPolicy(request.SandboxPolicy, SandboxPolicy.WorkspaceWrite),
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync(ct);

        ApplyBindings(agent, request.SkillIds, request.McpServerIds);
        await db.SaveChangesAsync(ct);

        var created = await LoadAgentAsync(agent.Id, ct);
        return Created($"/api/agents/{agent.Id}", ToDto(created!));
    }

    [HttpPut("api/agents/{id:guid}")]
    public async Task<ActionResult<AgentDetailDto>> Update(
        Guid id, [FromBody] UpdateAgentRequest request, CancellationToken ct)
    {
        var agent = await LoadAgentAsync(id, ct);
        if (agent is null)
        {
            return Problem(detail: "Agent 不存在", statusCode: 404);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "Agent 名称必填", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            return Problem(detail: "系统提示词必填", statusCode: 400);
        }

        if (!Enum.TryParse<AgentJob>(request.Job, ignoreCase: true, out var job))
        {
            return Problem(detail: $"无效的岗位 {request.Job}", statusCode: 400);
        }

        var bindingError = await ValidateBindingsAsync(
            request.SkillIds, request.McpServerIds, ct);
        if (bindingError is not null)
        {
            return Problem(detail: bindingError, statusCode: 400);
        }

        agent.Name = request.Name.Trim();
        agent.Job = job;
        agent.SystemPrompt = request.SystemPrompt;
        agent.Tools = request.Tools;
        agent.CommandWhitelist = request.CommandWhitelist;
        agent.SandboxPolicy = ParseSandboxPolicy(request.SandboxPolicy, agent.SandboxPolicy);
        agent.Enabled = request.Enabled;
        agent.UpdatedAt = DateTimeOffset.UtcNow;

        agent.Skills.Clear();
        agent.McpServers.Clear();
        ApplyBindings(agent, request.SkillIds, request.McpServerIds);
        await db.SaveChangesAsync(ct);

        var updated = await LoadAgentAsync(id, ct);
        return Ok(ToDto(updated!));
    }

    [HttpDelete("api/agents/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (agent is null)
        {
            return Problem(detail: "Agent 不存在", statusCode: 404);
        }

        db.Agents.Remove(agent);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("agent.delete", "agent", id.ToString(), ct: ct);
        return NoContent();
    }

    private async Task<Agent?> LoadAgentAsync(Guid id, CancellationToken ct) =>
        await db.Agents
            .Include(a => a.Provider)
            .Include(a => a.Skills)
            .Include(a => a.McpServers)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    private async Task<string?> ValidateBindingsAsync(
        IReadOnlyList<Guid>? skillIds,
        IReadOnlyList<Guid>? mcpServerIds,
        CancellationToken ct)
    {
        var skills = (skillIds ?? []).Distinct().ToList();
        if (skills.Count > 0)
        {
            var found = await db.Skills.CountAsync(s => skills.Contains(s.Id), ct);
            if (found != skills.Count)
            {
                return "包含不存在的 Skill";
            }
        }

        var servers = (mcpServerIds ?? []).Distinct().ToList();
        if (servers.Count > 0)
        {
            var found = await db.McpServers.CountAsync(m => servers.Contains(m.Id), ct);
            if (found != servers.Count)
            {
                return "包含不存在的 MCP Server";
            }
        }

        return null;
    }

    private void ApplyBindings(
        Agent agent,
        IReadOnlyList<Guid>? skillIds,
        IReadOnlyList<Guid>? mcpServerIds)
    {
        // 直接 Add 到 DbSet，确保新装配关系被标记为 Added（导航集合添加可能被判定为 Modified）
        foreach (var skillId in (skillIds ?? []).Distinct())
        {
            db.AgentSkills.Add(new AgentSkill { AgentId = agent.Id, SkillId = skillId });
        }

        foreach (var serverId in (mcpServerIds ?? []).Distinct())
        {
            db.AgentMcpServers.Add(new AgentMcpServer
            {
                AgentId = agent.Id,
                McpServerId = serverId,
            });
        }
    }

    private static AgentDetailDto ToDto(Agent agent) => new(
        agent.Id,
        agent.Name,
        agent.Job.ToString().ToLowerInvariant(),
        agent.ProviderId,
        agent.Provider.Name,
        agent.Model,
        agent.SystemPrompt,
        agent.Tools,
        agent.CommandWhitelist,
        agent.Enabled,
        agent.Skills.Select(s => s.SkillId).ToList(),
        agent.McpServers.Select(m => m.McpServerId).ToList(),
        agent.SandboxPolicy.ToString().ToLowerInvariant());

    private static SandboxPolicy ParseSandboxPolicy(string? value, SandboxPolicy fallback) =>
        Enum.TryParse<SandboxPolicy>(value, ignoreCase: true, out var policy) ? policy : fallback;
}
