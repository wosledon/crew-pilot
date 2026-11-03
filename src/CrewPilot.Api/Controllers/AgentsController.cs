using CrewPilot.Api.Contracts;
using CrewPilot.Core.Agents;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class AgentsController(CrewPilotDbContext db) : ControllerBase
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
        var agent = await db.Agents
            .AsNoTracking()
            .Include(a => a.Provider)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (agent is null)
        {
            return Problem(detail: "Agent 不存在", statusCode: 404);
        }

        return ToDto(agent);
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

        var agent = new Agent
        {
            Name = request.Name.Trim(),
            Job = job,
            ProviderId = provider.Id,
            Model = request.Model.Trim(),
            SystemPrompt = request.SystemPrompt,
            Tools = request.Tools,
            CommandWhitelist = request.CommandWhitelist,
        };
        db.Agents.Add(agent);
        await db.SaveChangesAsync(ct);

        agent.Provider = provider;
        return Created($"/api/agents/{agent.Id}", ToDto(agent));
    }

    [HttpPut("api/agents/{id:guid}")]
    public async Task<ActionResult<AgentDetailDto>> Update(
        Guid id, [FromBody] UpdateAgentRequest request, CancellationToken ct)
    {
        var agent = await db.Agents.Include(a => a.Provider).FirstOrDefaultAsync(a => a.Id == id, ct);
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

        agent.Name = request.Name.Trim();
        agent.Job = job;
        agent.SystemPrompt = request.SystemPrompt;
        agent.Tools = request.Tools;
        agent.CommandWhitelist = request.CommandWhitelist;
        agent.Enabled = request.Enabled;
        agent.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToDto(agent);
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
        return NoContent();
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
        agent.Enabled);
}
