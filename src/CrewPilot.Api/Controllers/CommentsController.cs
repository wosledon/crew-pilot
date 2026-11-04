using System.Text;
using System.Text.Json;
using CrewPilot.Agents.Execution;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Agents;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Projects;
using CrewPilot.Core.WorkItems;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>工作项评论：用户与 Agent 统一建模；评论中 @Agent 会触发一次 AgentRun 并把结果写回评论。</summary>
[ApiController]
public sealed class CommentsController(
    CrewPilotDbContext db,
    AgentRunQueue queue,
    ILogger<CommentsController> logger) : ControllerBase
{
    private const int MaxContentLength = 16_000;

    [HttpGet("api/workitems/{workItemId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> List(Guid workItemId, CancellationToken ct)
    {
        var exists = await db.WorkItems.AnyAsync(w => w.Id == workItemId, ct);
        if (!exists)
        {
            return Problem(detail: "工作项不存在", statusCode: 404);
        }

        var rows = await db.Comments
            .AsNoTracking()
            .Where(c => c.WorkItemId == workItemId)
            .Select(c => new
            {
                c.Id,
                c.WorkItemId,
                c.Content,
                AuthorName = c.Author != null ? c.Author.DisplayName : "系统",
                c.AuthorKind,
                c.CreatedAt,
            })
            .ToListAsync(ct);

        // SQLite 不支持 DateTimeOffset 排序，改为内存排序
        return Ok(rows
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(
                c.Id, c.WorkItemId, c.Content, c.AuthorName,
                c.AuthorKind.ToString().ToLowerInvariant(), c.CreatedAt))
            .ToList());
    }

    [HttpPost("api/workitems/{workItemId:guid}/comments")]
    public async Task<ActionResult<CreateCommentResultDto>> Create(
        Guid workItemId, [FromBody] CreateCommentRequest request, CancellationToken ct)
    {
        var content = (request.Content ?? string.Empty).Trim();
        if (content.Length is 0 or > MaxContentLength)
        {
            return Problem(detail: $"评论内容必填且不超过 {MaxContentLength} 个字符", statusCode: 400);
        }

        var workItem = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct);
        if (workItem is null)
        {
            return Problem(detail: "工作项不存在", statusCode: 404);
        }

        Agent? agent = null;
        if (request.MentionAgentId is Guid agentId && agentId != Guid.Empty)
        {
            agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == agentId && a.Enabled, ct);
            if (agent is null)
            {
                return Problem(detail: "Agent 不存在或已停用", statusCode: 400);
            }
        }

        var author = await EnsureMemberAsync(
            workItem.ProjectId, "我", MemberKind.User, ProjectRole.Owner, ct);

        var comment = new Comment
        {
            WorkItemId = workItem.Id,
            Author = author,
            AuthorId = author.Id,
            AuthorKind = MemberKind.User,
            Content = content,
        };
        db.Comments.Add(comment);
        AddActivity(workItem.ProjectId, workItem.Id, "comment.added",
            new { author = author.DisplayName, mention = agent?.Name }, MemberKind.User);

        AgentRun? run = null;
        if (agent is not null)
        {
            run = new AgentRun
            {
                AgentId = agent.Id,
                WorkItemId = workItem.Id,
                Trigger = "mention",
                Prompt = BuildMentionPrompt(agent, workItem, content),
                Status = AgentRunStatus.Queued,
            };
            db.AgentRuns.Add(run);
            AddActivity(workItem.ProjectId, workItem.Id, "agentrun.queued",
                new { agent = agent.Name, trigger = "mention" }, MemberKind.Agent);
        }

        await db.SaveChangesAsync(ct);

        if (run is not null)
        {
            await queue.EnqueueAsync(run.Id);
            logger.LogInformation(
                "评论触发 AgentRun：workItem={WorkItemId} agent={AgentId} run={RunId}",
                workItem.Id, agent!.Id, run.Id);
        }

        var dto = new CommentDto(
            comment.Id, comment.WorkItemId, comment.Content, author.DisplayName,
            comment.AuthorKind.ToString().ToLowerInvariant(), comment.CreatedAt);
        return Created($"/api/workitems/{workItemId}/comments/{comment.Id}",
            new CreateCommentResultDto(dto, run?.Id));
    }

    [HttpDelete("api/comments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var comment = await db.Comments
            .Include(c => c.WorkItem)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (comment is null)
        {
            return Problem(detail: "评论不存在", statusCode: 404);
        }

        if (comment.WorkItem is not null)
        {
            AddActivity(comment.WorkItem.ProjectId, comment.WorkItemId, "comment.deleted",
                new { commentId = comment.Id }, MemberKind.User);
        }

        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ProjectMember> EnsureMemberAsync(
        Guid projectId, string displayName, MemberKind kind, ProjectRole role, CancellationToken ct)
    {
        var member = await db.Members.FirstOrDefaultAsync(
            m => m.ProjectId == projectId && m.DisplayName == displayName, ct);
        if (member is not null)
        {
            return member;
        }

        member = new ProjectMember
        {
            ProjectId = projectId,
            Kind = kind,
            Role = role,
            DisplayName = displayName,
        };
        db.Members.Add(member);
        return member;
    }

    private void AddActivity(
        Guid projectId, Guid workItemId, string eventType, object payload, MemberKind actorKind)
    {
        db.Activities.Add(new Activity
        {
            ProjectId = projectId,
            WorkItemId = workItemId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            ActorKind = actorKind,
        });
    }

    private static string BuildMentionPrompt(Agent agent, WorkItem workItem, string comment)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"你是 {agent.Name}（岗位：{agent.Job}）。");
        sb.AppendLine("项目成员在评论中 @ 了你，请针对以下工作项与评论作出回应；");
        sb.AppendLine("如果需要更多信息，请明确列出需要人类补充的内容。");
        sb.AppendLine();
        sb.AppendLine($"工作项：{workItem.Title}");
        if (!string.IsNullOrWhiteSpace(workItem.Description))
        {
            sb.AppendLine($"描述：{workItem.Description}");
        }

        if (!string.IsNullOrWhiteSpace(workItem.AcceptanceCriteria))
        {
            sb.AppendLine($"验收标准：{workItem.AcceptanceCriteria}");
        }

        sb.AppendLine($"评论：{comment}");
        return sb.ToString();
    }
}
