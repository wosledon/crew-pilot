using CrewPilot.Api.Contracts;
using CrewPilot.Core.Agents;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>Agent 团队：一组 Agent 组成的小队（PRD 5.3）。</summary>
[ApiController]
public sealed class AgentTeamsController(CrewPilotDbContext db) : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/agent-teams")]
    public async Task<ActionResult<IReadOnlyList<AgentTeamDto>>> List(Guid projectId, CancellationToken ct)
    {
        var teams = await db.AgentTeams
            .AsNoTracking()
            .Include(t => t.Members).ThenInclude(m => m.Agent)
            .Where(t => t.ProjectId == projectId)
            .ToListAsync(ct);

        return Ok(teams
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList());
    }

    [HttpPost("api/projects/{projectId:guid}/agent-teams")]
    public async Task<ActionResult<AgentTeamDto>> Create(
        Guid projectId, [FromBody] CreateAgentTeamRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "团队名称必填", statusCode: 400);
        }

        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        var agentIds = (request.AgentIds ?? []).Distinct().ToList();
        if (agentIds.Count > 0)
        {
            var found = await db.Agents.CountAsync(a => agentIds.Contains(a.Id), ct);
            if (found != agentIds.Count)
            {
                return Problem(detail: "包含不存在的 Agent", statusCode: 400);
            }
        }

        var team = new AgentTeam
        {
            ProjectId = projectId,
            Name = request.Name.Trim(),
            Goal = request.Goal,
        };
        foreach (var agentId in agentIds)
        {
            team.Members.Add(new AgentTeamMember { AgentId = agentId });
        }

        db.AgentTeams.Add(team);
        await db.SaveChangesAsync(ct);

        var created = await db.AgentTeams
            .AsNoTracking()
            .Include(t => t.Members).ThenInclude(m => m.Agent)
            .FirstAsync(t => t.Id == team.Id, ct);
        return Created($"/api/agent-teams/{team.Id}", ToDto(created));
    }

    [HttpDelete("api/agent-teams/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var team = await db.AgentTeams.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (team is null)
        {
            return Problem(detail: "团队不存在", statusCode: 404);
        }

        db.AgentTeams.Remove(team);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static AgentTeamDto ToDto(AgentTeam team) => new(
        team.Id,
        team.ProjectId,
        team.Name,
        team.Goal,
        team.Members
            .OrderBy(m => m.Agent.Name, StringComparer.Ordinal)
            .Select(m => new AgentTeamMemberDto(m.AgentId, m.Agent.Name, m.Role))
            .ToList());
}
