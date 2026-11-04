using CrewPilot.Agents.Workflows;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Workflows;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>卡点队列：列出待处理卡点并处理（继续 / 修改方向 / 取消）。</summary>
[ApiController]
public sealed class BlockersController(
    CrewPilotDbContext db,
    WorkflowRunner runner) : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/blockers")]
    public async Task<ActionResult<IReadOnlyList<BlockerDto>>> List(
        Guid projectId, [FromQuery] string? status, CancellationToken ct)
    {
        var query = db.Blockers
            .AsNoTracking()
            .Include(b => b.WorkItem)
            .Where(b => b.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<BlockerStatus>(status, ignoreCase: true, out var blockerStatus))
        {
            query = query.Where(b => b.Status == blockerStatus);
        }
        else if (string.IsNullOrWhiteSpace(status))
        {
            // 默认只返回待处理
            query = query.Where(b => b.Status == BlockerStatus.Open);
        }

        var blockers = await query.ToListAsync(ct);
        return Ok(blockers
            .OrderByDescending(b => b.CreatedAt)
            .Select(ToDto)
            .ToList());
    }

    [HttpGet("api/blockers/{id:guid}")]
    public async Task<ActionResult<BlockerDto>> Get(Guid id, CancellationToken ct)
    {
        var blocker = await LoadAsync(id, ct);
        return blocker is null ? Problem(detail: "卡点不存在", statusCode: 404) : Ok(ToDto(blocker));
    }

    [HttpPost("api/blockers/{id:guid}/resolve")]
    public async Task<ActionResult<BlockerDto>> Resolve(
        Guid id, [FromBody] ResolveBlockerRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<BlockerResolution>(request.Resolution, ignoreCase: true, out var resolution))
        {
            return Problem(detail: "无效的处理方式（continue/redirect/cancel）", statusCode: 400);
        }

        var resolved = await runner.ResolveBlockerAsync(id, resolution, request.Note, ct);
        if (!resolved)
        {
            return Problem(detail: "卡点不存在或已处理", statusCode: 400);
        }

        var blocker = await LoadAsync(id, ct);
        return blocker is null ? Problem(detail: "卡点不存在", statusCode: 404) : Ok(ToDto(blocker));
    }

    private async Task<Blocker?> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Blockers
            .AsNoTracking()
            .Include(b => b.WorkItem)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

    private static BlockerDto ToDto(Blocker b) => new(
        b.Id,
        b.ProjectId,
        b.WorkItemId,
        b.WorkItem.Title,
        b.WorkflowRunId,
        b.AgentRunId,
        b.Kind.ToString().ToLowerInvariant(),
        b.Reason,
        b.ContextSnapshot,
        b.Status.ToString().ToLowerInvariant(),
        b.Resolution?.ToString().ToLowerInvariant(),
        b.ResolutionNote,
        b.CreatedAt,
        b.ResolvedAt);
}
