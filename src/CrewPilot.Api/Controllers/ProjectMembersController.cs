using System.Text.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Projects;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>项目成员：唯一 Owner + 可指派的 Agent 数字员工（人类与 Agent 统一建模）。</summary>
[ApiController]
public sealed class ProjectMembersController(CrewPilotDbContext db) : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> List(
        Guid projectId, CancellationToken ct)
    {
        var exists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!exists)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        var rows = await db.Members
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .Select(m => new ProjectMemberDto(
                m.Id, m.ProjectId, m.Kind.ToString().ToLowerInvariant(),
                m.Role.ToString().ToLowerInvariant(), m.DisplayName, m.CreatedAt))
            .ToListAsync(ct);

        return Ok(rows
            .OrderBy(m => m.Role == "owner" ? 0 : 1)
            .ThenBy(m => m.DisplayName, StringComparer.Ordinal)
            .ToList());
    }

    [HttpPost("api/projects/{projectId:guid}/members")]
    public async Task<ActionResult<ProjectMemberDto>> Add(
        Guid projectId, [FromBody] AddProjectMemberRequest request, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        MemberKind kind;
        string displayName;
        if (request.AgentId is Guid agentId && agentId != Guid.Empty)
        {
            var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == agentId, ct);
            if (agent is null)
            {
                return Problem(detail: "Agent 不存在", statusCode: 404);
            }

            kind = MemberKind.Agent;
            displayName = agent.Name;
        }
        else
        {
            displayName = (request.DisplayName ?? string.Empty).Trim();
            if (displayName.Length is 0 or > 100)
            {
                return Problem(detail: "成员名称必填且不超过 100 个字符", statusCode: 400);
            }

            kind = MemberKind.User;
        }

        var duplicate = await db.Members.AnyAsync(
            m => m.ProjectId == projectId && m.DisplayName == displayName, ct);
        if (duplicate)
        {
            return Problem(detail: $"成员 {displayName} 已存在", statusCode: 409);
        }

        var role = ParseRole(request.Role, ProjectRole.Developer);
        var member = new ProjectMember
        {
            ProjectId = projectId,
            Kind = kind,
            Role = role,
            DisplayName = displayName,
        };
        db.Members.Add(member);
        AddActivity(projectId, "member.added", new { member = displayName, kind = kind.ToString() });
        await db.SaveChangesAsync(ct);

        return Created($"/api/members/{member.Id}", ToDto(member));
    }

    [HttpPut("api/members/{id:guid}")]
    public async Task<ActionResult<ProjectMemberDto>> Update(
        Guid id, [FromBody] UpdateProjectMemberRequest request, CancellationToken ct)
    {
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member is null)
        {
            return Problem(detail: "成员不存在", statusCode: 404);
        }

        var displayName = (request.DisplayName ?? string.Empty).Trim();
        if (displayName.Length is 0 or > 100)
        {
            return Problem(detail: "成员名称必填且不超过 100 个字符", statusCode: 400);
        }

        var role = ParseRole(request.Role, member.Role);
        if (member.Role == ProjectRole.Owner && role != ProjectRole.Owner)
        {
            return Problem(detail: "项目必须有 Owner，不能变更 Owner 的角色", statusCode: 400);
        }

        member.DisplayName = displayName;
        member.Role = role;
        AddActivity(member.ProjectId, "member.updated", new { member = displayName, role = role.ToString() });
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(member));
    }

    [HttpDelete("api/members/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (member is null)
        {
            return Problem(detail: "成员不存在", statusCode: 404);
        }

        if (member.Role == ProjectRole.Owner)
        {
            return Problem(detail: "不能移除项目 Owner", statusCode: 400);
        }

        // 评论外键是级联删除，先拦截有发言历史的成员，避免误删内容
        var hasComments = await db.Comments.AnyAsync(c => c.AuthorId == id, ct);
        if (hasComments)
        {
            return Problem(detail: "该成员已有评论记录，无法移除（可改为停用）", statusCode: 400);
        }

        db.Members.Remove(member);
        AddActivity(member.ProjectId, "member.removed", new { member = member.DisplayName });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private void AddActivity(Guid projectId, string eventType, object payload)
    {
        db.Activities.Add(new Activity
        {
            ProjectId = projectId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            ActorKind = MemberKind.User,
        });
    }

    private static ProjectRole ParseRole(string? value, ProjectRole fallback) =>
        Enum.TryParse<ProjectRole>(value, ignoreCase: true, out var role) ? role : fallback;

    private static ProjectMemberDto ToDto(ProjectMember member) => new(
        member.Id, member.ProjectId, member.Kind.ToString().ToLowerInvariant(),
        member.Role.ToString().ToLowerInvariant(), member.DisplayName, member.CreatedAt);
}
