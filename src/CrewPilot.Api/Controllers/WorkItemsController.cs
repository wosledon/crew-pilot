using System.Text.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Projects;
using CrewPilot.Core.Services;
using CrewPilot.Core.WorkItems;
using CrewPilot.Core.WorkLogs;
using CrewPilot.Infrastructure.Audit;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

[ApiController]
public sealed class WorkItemsController(
    CrewPilotDbContext db,
    AuditLogger audit,
    IRealtimeNotifier realtime) : ControllerBase
{
    /// <summary>工作项列表，支持 type（requirement/defect/task）与 status 过滤。</summary>
    [HttpGet("api/projects/{projectId:guid}/workitems")]
    public async Task<ActionResult<IReadOnlyList<WorkItemListItemDto>>> List(
        Guid projectId,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] string? tag,
        CancellationToken ct)
    {
        var rows = await QueryRowsAsync(projectId, type, ct);

        if (!string.IsNullOrWhiteSpace(status))
        {
            rows = rows
                .Where(r => string.Equals(r.Status, status.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var wanted = tag.Trim();
            rows = rows
                .Where(r => r.Tags is not null && r.Tags
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(t => string.Equals(t, wanted, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var loggedHours = await GetLoggedHoursAsync(rows.Select(r => r.Id), ct);
        var items = rows
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new WorkItemListItemDto(
                r.Id, r.Type, r.Title, r.Status,
                r.Priority.ToString().ToLowerInvariant(), r.AssigneeName,
                r.EstimatedHours,
                loggedHours.TryGetValue(r.Id, out var hours) ? hours : 0m,
                r.RemainingHours, r.DueDate, r.PrUrl, r.UpdatedAt, r.Tags))
            .ToList();
        return Ok(items);
    }

    [HttpGet("api/workitems/{id:guid}")]
    public async Task<ActionResult<WorkItemDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var dto = await GetDetailAsync(id, ct);
        return dto is null ? Problem(detail: "工作项不存在", statusCode: 404) : Ok(dto);
    }

    [HttpPost("api/projects/{projectId:guid}/workitems")]
    public async Task<ActionResult<WorkItemDetailDto>> Create(
        Guid projectId, [FromBody] CreateWorkItemRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Problem(detail: "标题必填", statusCode: 400);
        }

        var type = (request.Type ?? string.Empty).Trim().ToLowerInvariant();
        WorkItem? workItem = type switch
        {
            "requirement" => new Requirement { Status = RequirementStatus.Backlog },
            "defect" => new Defect
            {
                Status = DefectStatus.New,
                Severity = ParseSeverity(request.Severity),
                ReproSteps = request.ReproSteps,
                Environment = request.Environment,
            },
            "task" => new TaskItem(),
            _ => null,
        };
        if (workItem is null)
        {
            return Problem(detail: $"不支持的工作项类型 {request.Type}（requirement/defect/task）", statusCode: 400);
        }

        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        workItem.ProjectId = projectId;
        workItem.Title = request.Title.Trim();
        workItem.Description = request.Description;
        workItem.AcceptanceCriteria = request.AcceptanceCriteria;
        workItem.Tags = NormalizeTags(request.Tags);
        workItem.Priority = ParsePriority(request.Priority);
        workItem.StoryPoints = request.StoryPoints;
        workItem.EstimatedHours = request.EstimatedHours;
        workItem.RemainingHours = request.RemainingHours;
        workItem.DueDate = request.DueDate;
        workItem.IterationId = await ResolveIterationAsync(projectId, request.IterationId, ct);
        workItem.Assignee = await ResolveAssigneeAsync(projectId, request.AssigneeName, ct);

        if (workItem is TaskItem task)
        {
            var parentId = await ResolveParentAsync(projectId, request.ParentWorkItemId, ct);
            if (request.ParentWorkItemId is Guid requestedParent && parentId is null)
            {
                return Problem(detail: "父工作项不存在或不属于该项目", statusCode: 400);
            }

            task.ParentWorkItemId = parentId ?? Guid.Empty;
        }

        if (workItem is Defect defect)
        {
            var (relatedId, relatedError) = await ResolveRelatedRequirementAsync(
                projectId, request.RelatedRequirementId, ct);
            if (relatedError is not null)
            {
                return Problem(detail: relatedError, statusCode: 400);
            }

            defect.RelatedRequirementId = relatedId;
        }

        db.WorkItems.Add(workItem);
        await AddActivityAsync(projectId, workItem.Id, "workitem.created", new { type, workItem.Title }, ct);
        await db.SaveChangesAsync(ct);

        var dto = await GetDetailAsync(workItem.Id, ct);
        await realtime.WorkItemChangedAsync(projectId, workItem.Id, dto!.Status, ct);
        return Created($"/api/workitems/{workItem.Id}", dto);
    }

    [HttpPut("api/workitems/{id:guid}")]
    public async Task<ActionResult<WorkItemDetailDto>> Update(
        Guid id, [FromBody] UpdateWorkItemRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Problem(detail: "标题必填", statusCode: 400);
        }

        var workItem = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workItem is null)
        {
            return Problem(detail: "工作项不存在", statusCode: 404);
        }

        workItem.Title = request.Title.Trim();
        workItem.Description = request.Description;
        workItem.AcceptanceCriteria = request.AcceptanceCriteria;
        workItem.Tags = NormalizeTags(request.Tags);
        workItem.Priority = ParsePriority(request.Priority);
        workItem.StoryPoints = request.StoryPoints;
        workItem.EstimatedHours = request.EstimatedHours;
        workItem.RemainingHours = request.RemainingHours;
        workItem.DueDate = request.DueDate;
        workItem.IterationId = await ResolveIterationAsync(workItem.ProjectId, request.IterationId, ct);
        workItem.Assignee = await ResolveAssigneeAsync(workItem.ProjectId, request.AssigneeName, ct);
        workItem.BranchUrl = string.IsNullOrWhiteSpace(request.BranchUrl) ? null : request.BranchUrl.Trim();
        workItem.PrUrl = string.IsNullOrWhiteSpace(request.PrUrl) ? null : request.PrUrl.Trim();

        switch (workItem)
        {
            case Defect defect:
                defect.Severity = ParseSeverity(request.Severity);
                defect.ReproSteps = request.ReproSteps;
                defect.Environment = request.Environment;
                var (relatedId, relatedError) = await ResolveRelatedRequirementAsync(
                    workItem.ProjectId, request.RelatedRequirementId, ct);
                if (relatedError is not null)
                {
                    return Problem(detail: relatedError, statusCode: 400);
                }

                defect.RelatedRequirementId = relatedId;
                break;
            case TaskItem task:
                var parentId = await ResolveParentAsync(workItem.ProjectId, request.ParentWorkItemId, ct);
                if (request.ParentWorkItemId is Guid requestedParent && parentId is null)
                {
                    return Problem(detail: "父工作项不存在或不属于该项目", statusCode: 400);
                }

                task.ParentWorkItemId = parentId ?? Guid.Empty;
                break;
        }

        workItem.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var dto = await GetDetailAsync(id, ct);
        await realtime.WorkItemChangedAsync(workItem.ProjectId, workItem.Id, dto!.Status, ct);
        return Ok(dto);
    }

    /// <summary>状态流转：必须走领域状态机，非法流转返回 400。</summary>
    [HttpPost("api/workitems/{id:guid}/status")]
    public async Task<ActionResult<WorkItemDetailDto>> Transition(
        Guid id, [FromBody] TransitionRequest request, CancellationToken ct)
    {
        var workItem = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workItem is null)
        {
            return Problem(detail: "工作项不存在", statusCode: 404);
        }

        var to = (request.ToStatus ?? string.Empty).Trim();
        var result = ApplyTransition(workItem, to);
        if (!result.Ok)
        {
            return Problem(detail: result.Error, statusCode: 400);
        }

        workItem.UpdatedAt = DateTimeOffset.UtcNow;
        await AddActivityAsync(
            workItem.ProjectId, workItem.Id, "workitem.status.changed",
            new { from = result.From, to = result.To }, ct);
        await db.SaveChangesAsync(ct);

        var dto = await GetDetailAsync(id, ct);
        await realtime.WorkItemChangedAsync(workItem.ProjectId, workItem.Id, result.To, ct);
        return Ok(dto);
    }

    /// <summary>批量操作：状态流转 / 指派 / 删除（逐项返回结果，失败不中断其余项）。</summary>
    [HttpPost("api/workitems/batch")]
    public async Task<ActionResult<BatchWorkItemsResultDto>> Batch(
        [FromBody] BatchWorkItemsRequest request, CancellationToken ct)
    {
        var ids = (request.Ids ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            return Problem(detail: "请选择工作项", statusCode: 400);
        }

        if (ids.Count > 100)
        {
            return Problem(detail: "单次最多操作 100 个工作项", statusCode: 400);
        }

        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
        if (action is not ("status" or "assign" or "delete"))
        {
            return Problem(detail: "批量动作无效（status/assign/delete）", statusCode: 400);
        }

        var items = await db.WorkItems
            .Where(w => ids.Contains(w.Id))
            .ToListAsync(ct);
        var byId = items.ToDictionary(w => w.Id);
        var results = new List<BatchItemResultDto>();
        var changed = new List<(Guid ProjectId, Guid Id, string Status)>();

        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var item))
            {
                results.Add(new BatchItemResultDto(id, false, "工作项不存在"));
                continue;
            }

            switch (action)
            {
                case "status":
                {
                    var result = ApplyTransition(item, (request.ToStatus ?? string.Empty).Trim());
                    if (!result.Ok)
                    {
                        results.Add(new BatchItemResultDto(id, false, result.Error));
                        break;
                    }

                    item.UpdatedAt = DateTimeOffset.UtcNow;
                    await AddActivityAsync(
                        item.ProjectId, item.Id, "workitem.status.changed",
                        new { from = result.From, to = result.To, batch = true }, ct);
                    results.Add(new BatchItemResultDto(id, true, null));
                    changed.Add((item.ProjectId, item.Id, result.To));
                    break;
                }

                case "assign":
                {
                    item.Assignee = await ResolveAssigneeAsync(
                        item.ProjectId, request.AssigneeName, ct);
                    item.UpdatedAt = DateTimeOffset.UtcNow;
                    await AddActivityAsync(
                        item.ProjectId, item.Id, "workitem.status.changed",
                        new { assignee = request.AssigneeName, batch = true }, ct);
                    results.Add(new BatchItemResultDto(id, true, null));
                    changed.Add((item.ProjectId, item.Id, "updated"));
                    break;
                }

                default:
                {
                    db.WorkItems.Remove(item);
                    results.Add(new BatchItemResultDto(id, true, null));
                    changed.Add((item.ProjectId, item.Id, "deleted"));
                    break;
                }
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (var (projectId, itemId, status) in changed)
        {
            await realtime.WorkItemChangedAsync(projectId, itemId, status, ct);
            if (status == "deleted")
            {
                await audit.LogAsync(
                    "workitem.batch.delete", "workitem", itemId.ToString(), projectId, ct: ct);
            }
        }

        var succeeded = results.Count(r => r.Ok);
        return Ok(new BatchWorkItemsResultDto(succeeded, results.Count - succeeded, results));
    }

    [HttpDelete("api/workitems/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var workItem = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workItem is null)
        {
            return Problem(detail: "工作项不存在", statusCode: 404);
        }

        db.WorkItems.Remove(workItem);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(
            "workitem.delete", "workitem", id.ToString(), workItem.ProjectId,
            workItem.Title, ct);
        await realtime.WorkItemChangedAsync(
            workItem.ProjectId, workItem.Id, "deleted", ct);
        return NoContent();
    }

    [HttpGet("api/workitems/{id:guid}/worklogs")]
    public async Task<ActionResult<IReadOnlyList<WorkLogDto>>> ListWorkLogs(Guid id, CancellationToken ct)
    {
        var logs = await db.WorkLogs
            .AsNoTracking()
            .Where(l => l.WorkItemId == id)
            .OrderByDescending(l => l.Date)
            .Select(l => new WorkLogDto(
                l.Id, l.Hours, l.Date, l.Note,
                l.Member != null ? l.Member.DisplayName : null, l.AgentRunId, l.CreatedAt))
            .ToListAsync(ct);
        return Ok(logs);
    }

    /// <summary>登记工时（1:1，不加权）。</summary>
    [HttpPost("api/workitems/{id:guid}/worklogs")]
    public async Task<ActionResult<WorkLogDto>> AddWorkLog(
        Guid id, [FromBody] CreateWorkLogRequest request, CancellationToken ct)
    {
        if (request.Hours <= 0 || request.Hours > 24)
        {
            return Problem(detail: "工时必须大于 0 且不超过 24 小时", statusCode: 400);
        }

        var workItem = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workItem is null)
        {
            return Problem(detail: "工作项不存在", statusCode: 404);
        }

        var owner = await EnsureOwnerMemberAsync(workItem.ProjectId, ct);
        var log = new WorkLog
        {
            WorkItemId = id,
            MemberId = owner.Id,
            Hours = request.Hours,
            Date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Note = request.Note,
        };

        // 剩余工时随登记自动扣减（不为负）
        if (workItem.RemainingHours is decimal remaining)
        {
            workItem.RemainingHours = Math.Max(0m, remaining - request.Hours);
        }

        db.WorkLogs.Add(log);
        await AddActivityAsync(workItem.ProjectId, id, "worklog.added", new { log.Hours }, ct);
        await db.SaveChangesAsync(ct);

        return Created($"/api/workitems/{id}/worklogs/{log.Id}",
            new WorkLogDto(log.Id, log.Hours, log.Date, log.Note, owner.DisplayName, log.AgentRunId, log.CreatedAt));
    }

    private async Task<WorkItemDetailDto?> GetDetailAsync(Guid id, CancellationToken ct)
    {
        var row = await QueryRowAsync(id, ct);
        if (row is null)
        {
            return null;
        }

        var loggedHours = await GetLoggedHoursAsync([row.Id], ct);
        Guid? relatedRequirementId = null;
        string? relatedRequirementTitle = null;
        if (row.Type == "defect")
        {
            relatedRequirementId = await db.WorkItems
                .OfType<Defect>()
                .AsNoTracking()
                .Where(d => d.Id == id)
                .Select(d => d.RelatedRequirementId)
                .FirstOrDefaultAsync(ct);
            if (relatedRequirementId is Guid requirementId)
            {
                relatedRequirementTitle = await db.WorkItems
                    .OfType<Requirement>()
                    .AsNoTracking()
                    .Where(r => r.Id == requirementId)
                    .Select(r => r.Title)
                    .FirstOrDefaultAsync(ct);
            }
        }

        return new WorkItemDetailDto(
            row.Id, row.Type, row.Title, row.Status,
            row.Priority.ToString().ToLowerInvariant(), row.Description, row.AcceptanceCriteria,
            row.Severity, row.ReproSteps, row.Environment,
            row.StoryPoints, row.EstimatedHours,
            loggedHours.TryGetValue(row.Id, out var hours) ? hours : 0m,
            row.RemainingHours, row.AssigneeName, row.IterationId, row.DueDate, row.BranchUrl, row.PrUrl,
            row.CreatedAt, row.UpdatedAt, row.Tags, row.ParentWorkItemId,
            relatedRequirementId, relatedRequirementTitle);
    }

    /// <summary>工作项查询行（跨 TPH 三类统一视图）。</summary>
    private sealed record WorkItemRow(
        Guid Id,
        string Type,
        string Title,
        string Status,
        Priority Priority,
        string? Description,
        string? AcceptanceCriteria,
        string? Severity,
        string? ReproSteps,
        string? Environment,
        int? StoryPoints,
        decimal? EstimatedHours,
        decimal? RemainingHours,
        string? AssigneeName,
        Guid? IterationId,
        DateOnly? DueDate,
        string? BranchUrl,
        string? PrUrl,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        string? Tags,
        Guid? ParentWorkItemId);
    private const int MaxTagsLength = 500;

    /// <summary>跨 TPH 三类工作项查询（需求/缺陷各自带状态列，任务无状态）。</summary>
    private async Task<List<WorkItemRow>> QueryRowsAsync(Guid projectId, string? typeFilter, CancellationToken ct)
    {
        var rows = new List<WorkItemRow>();
        var wantAll = string.IsNullOrWhiteSpace(typeFilter);
        var type = typeFilter?.Trim().ToLowerInvariant();

        if (wantAll || type == "requirement")
        {
            var reqs = await db.WorkItems
                .OfType<Requirement>()
                .AsNoTracking()
                .Where(w => w.ProjectId == projectId)
                .Select(w => new
                {
                    w.Id,
                    w.Title,
                    w.Status,
                    w.Priority,
                    w.Description,
                    w.AcceptanceCriteria,
                    w.StoryPoints,
                    w.EstimatedHours,
                    w.RemainingHours,
                    AssigneeName = w.Assignee != null ? w.Assignee.DisplayName : null,
                    w.IterationId,
                    w.DueDate,
                    w.BranchUrl,
                    w.PrUrl,
                    w.CreatedAt,
                    w.UpdatedAt,
                    w.Tags,
                })
                .ToListAsync(ct);
            rows.AddRange(reqs.Select(x => new WorkItemRow(
                x.Id, "requirement", x.Title, x.Status.ToString().ToLowerInvariant(), x.Priority,
                x.Description, x.AcceptanceCriteria, null, null, null,
                x.StoryPoints, x.EstimatedHours, x.RemainingHours, x.AssigneeName,
                x.IterationId, x.DueDate, x.BranchUrl, x.PrUrl, x.CreatedAt, x.UpdatedAt,
                x.Tags, null)));
        }

        if (wantAll || type == "defect")
        {
            var defects = await db.WorkItems
                .OfType<Defect>()
                .AsNoTracking()
                .Where(w => w.ProjectId == projectId)
                .Select(w => new
                {
                    w.Id,
                    w.Title,
                    w.Status,
                    w.Severity,
                    w.Priority,
                    w.Description,
                    w.AcceptanceCriteria,
                    w.ReproSteps,
                    w.Environment,
                    w.StoryPoints,
                    w.EstimatedHours,
                    w.RemainingHours,
                    AssigneeName = w.Assignee != null ? w.Assignee.DisplayName : null,
                    w.IterationId,
                    w.DueDate,
                    w.BranchUrl,
                    w.PrUrl,
                    w.CreatedAt,
                    w.UpdatedAt,
                    w.Tags,
                })
                .ToListAsync(ct);
            rows.AddRange(defects.Select(x => new WorkItemRow(
                x.Id, "defect", x.Title, x.Status.ToString().ToLowerInvariant(), x.Priority,
                x.Description, x.AcceptanceCriteria, x.Severity.ToString().ToLowerInvariant(),
                x.ReproSteps, x.Environment,
                x.StoryPoints, x.EstimatedHours, x.RemainingHours, x.AssigneeName,
                x.IterationId, x.DueDate, x.BranchUrl, x.PrUrl, x.CreatedAt, x.UpdatedAt,
                x.Tags, null)));
        }

        if (wantAll || type == "task")
        {
            var tasks = await db.WorkItems
                .OfType<TaskItem>()
                .AsNoTracking()
                .Where(w => w.ProjectId == projectId)
                .Select(w => new
                {
                    w.Id,
                    w.Title,
                    w.Priority,
                    w.Description,
                    w.AcceptanceCriteria,
                    w.StoryPoints,
                    w.EstimatedHours,
                    w.RemainingHours,
                    AssigneeName = w.Assignee != null ? w.Assignee.DisplayName : null,
                    w.IterationId,
                    w.DueDate,
                    w.BranchUrl,
                    w.PrUrl,
                    w.CreatedAt,
                    w.UpdatedAt,
                    w.Tags,
                    w.IsDone,
                    w.ParentWorkItemId,
                })
                .ToListAsync(ct);
            rows.AddRange(tasks.Select(x => new WorkItemRow(
                x.Id, "task", x.Title, x.IsDone ? "done" : "todo", x.Priority,
                x.Description, x.AcceptanceCriteria, null, null, null,
                x.StoryPoints, x.EstimatedHours, x.RemainingHours, x.AssigneeName,
                x.IterationId, x.DueDate, x.BranchUrl, x.PrUrl, x.CreatedAt, x.UpdatedAt,
                x.Tags, x.ParentWorkItemId == Guid.Empty ? null : x.ParentWorkItemId)));
        }

        return rows;
    }

    private async Task<WorkItemRow?> QueryRowAsync(Guid id, CancellationToken ct)
    {
        var req = await db.WorkItems
            .OfType<Requirement>()
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new
            {
                w.Id,
                w.Title,
                w.Status,
                w.Priority,
                w.Description,
                w.AcceptanceCriteria,
                w.StoryPoints,
                w.EstimatedHours,
                w.RemainingHours,
                AssigneeName = w.Assignee != null ? w.Assignee.DisplayName : null,
                w.IterationId,
                w.DueDate,
                w.BranchUrl,
                w.PrUrl,
                w.CreatedAt,
                w.UpdatedAt,
                w.Tags,
            })
            .FirstOrDefaultAsync(ct);
        if (req is not null)
        {
            return new WorkItemRow(
                req.Id, "requirement", req.Title, req.Status.ToString().ToLowerInvariant(), req.Priority,
                req.Description, req.AcceptanceCriteria, null, null, null,
                req.StoryPoints, req.EstimatedHours, req.RemainingHours, req.AssigneeName,
                req.IterationId, req.DueDate, req.BranchUrl, req.PrUrl, req.CreatedAt, req.UpdatedAt,
                req.Tags, null);
        }

        var defect = await db.WorkItems
            .OfType<Defect>()
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new
            {
                w.Id,
                w.Title,
                w.Status,
                w.Severity,
                w.Priority,
                w.Description,
                w.AcceptanceCriteria,
                w.ReproSteps,
                w.Environment,
                w.StoryPoints,
                w.EstimatedHours,
                w.RemainingHours,
                AssigneeName = w.Assignee != null ? w.Assignee.DisplayName : null,
                w.IterationId,
                w.DueDate,
                w.BranchUrl,
                w.PrUrl,
                w.CreatedAt,
                w.UpdatedAt,
                w.Tags,
            })
            .FirstOrDefaultAsync(ct);
        if (defect is not null)
        {
            return new WorkItemRow(
                defect.Id, "defect", defect.Title, defect.Status.ToString().ToLowerInvariant(), defect.Priority,
                defect.Description, defect.AcceptanceCriteria, defect.Severity.ToString().ToLowerInvariant(),
                defect.ReproSteps, defect.Environment,
                defect.StoryPoints, defect.EstimatedHours, defect.RemainingHours, defect.AssigneeName,
                defect.IterationId, defect.DueDate, defect.BranchUrl, defect.PrUrl, defect.CreatedAt, defect.UpdatedAt,
                defect.Tags, null);
        }

        var task = await db.WorkItems
            .OfType<TaskItem>()
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(w => new
            {
                w.Id,
                w.Title,
                w.Priority,
                w.Description,
                w.AcceptanceCriteria,
                w.StoryPoints,
                w.EstimatedHours,
                w.RemainingHours,
                AssigneeName = w.Assignee != null ? w.Assignee.DisplayName : null,
                w.IterationId,
                w.DueDate,
                w.BranchUrl,
                w.PrUrl,
                w.CreatedAt,
                w.UpdatedAt,
                w.Tags,
                w.IsDone,
                w.ParentWorkItemId,
            })
            .FirstOrDefaultAsync(ct);

        return task is null
            ? null
            : new WorkItemRow(
                task.Id, "task", task.Title, task.IsDone ? "done" : "todo", task.Priority,
                task.Description, task.AcceptanceCriteria, null, null, null,
                task.StoryPoints, task.EstimatedHours, task.RemainingHours, task.AssigneeName,
                task.IterationId, task.DueDate, task.BranchUrl, task.PrUrl, task.CreatedAt, task.UpdatedAt,
                task.Tags, task.ParentWorkItemId == Guid.Empty ? null : task.ParentWorkItemId);
    }

    /// <summary>校验并解析父工作项：必须存在且属于同一项目。</summary>
    private async Task<Guid?> ResolveParentAsync(
        Guid projectId, Guid? parentWorkItemId, CancellationToken ct)
    {
        if (parentWorkItemId is null || parentWorkItemId == Guid.Empty)
        {
            return null;
        }

        var exists = await db.WorkItems.AnyAsync(
            w => w.Id == parentWorkItemId && w.ProjectId == projectId, ct);
        return exists ? parentWorkItemId : null;
    }

    /// <summary>校验并解析缺陷关联需求：必须存在且属于同一项目。</summary>
    private async Task<(Guid? Id, string? Error)> ResolveRelatedRequirementAsync(
        Guid projectId, Guid? relatedRequirementId, CancellationToken ct)
    {
        if (relatedRequirementId is null || relatedRequirementId == Guid.Empty)
        {
            return (null, null);
        }

        var exists = await db.WorkItems
            .OfType<Requirement>()
            .AnyAsync(r => r.Id == relatedRequirementId && r.ProjectId == projectId, ct);
        return exists
            ? (relatedRequirementId, null)
            : (null, "关联需求不存在或不属于该项目");
    }

    /// <summary>单用户产品：工时登记人默认为 Owner 本人。</summary>
    private async Task<ProjectMember> EnsureOwnerMemberAsync(Guid projectId, CancellationToken ct)
    {
        var member = await db.Members.FirstOrDefaultAsync(
            m => m.ProjectId == projectId && m.DisplayName == "我", ct);
        if (member is not null)
        {
            return member;
        }

        member = new ProjectMember
        {
            ProjectId = projectId,
            Kind = MemberKind.User,
            Role = ProjectRole.Owner,
            DisplayName = "我",
        };
        db.Members.Add(member);
        return member;
    }

    private static string? NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return null;
        }

        var parts = tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        var normalized = string.Join(',', parts);
        return normalized.Length > MaxTagsLength ? normalized[..MaxTagsLength] : normalized;
    }

    private async Task<Dictionary<Guid, decimal>> GetLoggedHoursAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        var rows = await db.WorkLogs
            .Where(l => idList.Contains(l.WorkItemId))
            .GroupBy(l => l.WorkItemId)
            .Select(g => new { Id = g.Key, Total = g.Sum(l => l.Hours) })
            .ToListAsync(ct);
        return rows.ToDictionary(x => x.Id, x => x.Total);
    }

    /// <summary>校验并解析迭代归属：迭代必须存在且属于同一项目。</summary>
    private async Task<Guid?> ResolveIterationAsync(Guid projectId, Guid? iterationId, CancellationToken ct)
    {
        if (iterationId is null || iterationId == Guid.Empty)
        {
            return null;
        }

        var exists = await db.Iterations.AnyAsync(
            i => i.Id == iterationId && i.ProjectId == projectId, ct);
        return exists ? iterationId : null;
    }

    private async Task<ProjectMember?> ResolveAssigneeAsync(Guid projectId, string? name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        var member = await db.Members
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.DisplayName == trimmed, ct);
        if (member is not null)
        {
            return member;
        }

        member = new ProjectMember
        {
            ProjectId = projectId,
            Kind = MemberKind.User,
            Role = ProjectRole.Developer,
            DisplayName = trimmed,
        };
        db.Members.Add(member);
        return member;
    }

    private static (bool Ok, string? Error, string From, string To) ApplyTransition(
        WorkItem workItem, string to)
    {
        try
        {
            switch (workItem)
            {
                case Requirement requirement:
                    if (!Enum.TryParse<RequirementStatus>(to, ignoreCase: true, out var rTarget))
                    {
                        return (false, $"无效的需求状态 {to}", string.Empty, string.Empty);
                    }

                    var rFrom = requirement.Status.ToString();
                    WorkItemStateMachine.Transition(requirement, rTarget);
                    return (true, null, rFrom, requirement.Status.ToString());

                case Defect defect:
                    if (!Enum.TryParse<DefectStatus>(to, ignoreCase: true, out var dTarget))
                    {
                        return (false, $"无效的缺陷状态 {to}", string.Empty, string.Empty);
                    }

                    var dFrom = defect.Status.ToString();
                    WorkItemStateMachine.Transition(defect, dTarget);
                    return (true, null, dFrom, defect.Status.ToString());

                case TaskItem task:
                    if (!string.Equals(to, "todo", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(to, "done", StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"无效的任务状态 {to}（todo/done）", string.Empty, string.Empty);
                    }

                    var tFrom = WorkItemStateMachine.ToStatus(task);
                    WorkItemStateMachine.Transition(
                        task, string.Equals(to, "done", StringComparison.OrdinalIgnoreCase));
                    return (true, null, tFrom, WorkItemStateMachine.ToStatus(task));

                default:
                    return (false, "不支持的工作项类型", string.Empty, string.Empty);
            }
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message, string.Empty, string.Empty);
        }
    }

    private async Task AddActivityAsync(
        Guid projectId, Guid? workItemId, string eventType, object payload, CancellationToken ct)
    {
        db.Activities.Add(new Activity
        {
            ProjectId = projectId,
            WorkItemId = workItemId,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            ActorKind = MemberKind.User,
        });
        await Task.CompletedTask;
    }

    private static Priority ParsePriority(string? value) =>
        Enum.TryParse<Priority>(value, ignoreCase: true, out var priority) ? priority : Priority.P2;

    private static Severity ParseSeverity(string? value) =>
        Enum.TryParse<Severity>(value, ignoreCase: true, out var severity) ? severity : Severity.Normal;
}
