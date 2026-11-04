using CrewPilot.Agents.Workflows;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Agents;
using CrewPilot.Core.Workflows;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>工作流定义、模板实例化与运行管理。</summary>
[ApiController]
public sealed class WorkflowsController(
    CrewPilotDbContext db,
    WorkflowRunner runner) : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/workflows")]
    public async Task<ActionResult<IReadOnlyList<WorkflowDto>>> List(Guid projectId, CancellationToken ct)
    {
        var workflows = await db.Workflows
            .AsNoTracking()
            .Include(w => w.Steps).ThenInclude(s => s.Agent)
            .Where(w => w.ProjectId == projectId && !w.IsTemplate)
            .ToListAsync(ct);

        return Ok(workflows
            .OrderBy(w => w.Name, StringComparer.Ordinal)
            .Select(ToDto)
            .ToList());
    }

    [HttpGet("api/workflows/templates")]
    public ActionResult<IReadOnlyList<WorkflowTemplateDto>> Templates()
    {
        return Ok(WorkflowTemplateCatalog.All
            .Select(template => new WorkflowTemplateDto(
                template.Key,
                template.Name,
                template.Description,
                template.Steps.Select(step => new WorkflowTemplateStepDto(
                    step.Name,
                    step.Job.ToString().ToLowerInvariant(),
                    step.Instruction,
                    step.ApprovalPolicy.ToString().ToLowerInvariant(),
                    step.MaxRetries)).ToList()))
            .ToList());
    }

    [HttpGet("api/workflows/{id:guid}")]
    public async Task<ActionResult<WorkflowDto>> Get(Guid id, CancellationToken ct)
    {
        var workflow = await LoadWorkflowAsync(id, ct);
        return workflow is null
            ? Problem(detail: "工作流不存在", statusCode: 404)
            : Ok(ToDto(workflow));
    }

    [HttpPost("api/projects/{projectId:guid}/workflows")]
    public async Task<ActionResult<WorkflowDto>> Create(
        Guid projectId, [FromBody] CreateWorkflowRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "工作流名称必填", statusCode: 400);
        }

        var exists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!exists)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        var error = await ValidateStepsAsync(request.Steps, ct);
        if (error is not null)
        {
            return Problem(detail: error, statusCode: 400);
        }

        var workflow = new Workflow
        {
            ProjectId = projectId,
            Name = request.Name.Trim(),
            Description = request.Description,
        };
        ApplySteps(workflow, request.Steps);
        db.Workflows.Add(workflow);
        await db.SaveChangesAsync(ct);

        var created = await LoadWorkflowAsync(workflow.Id, ct);
        return Created($"/api/workflows/{workflow.Id}", ToDto(created!));
    }

    [HttpPost("api/projects/{projectId:guid}/workflows/from-template")]
    public async Task<ActionResult<WorkflowDto>> CreateFromTemplate(
        Guid projectId, [FromBody] CreateWorkflowFromTemplateRequest request, CancellationToken ct)
    {
        var template = WorkflowTemplateCatalog.Find(request.TemplateKey);
        if (template is null)
        {
            return Problem(detail: $"未知模板 {request.TemplateKey}", statusCode: 400);
        }

        var exists = await db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!exists)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        var workflow = new Workflow
        {
            ProjectId = projectId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? template.Name : request.Name.Trim(),
            Description = template.Description,
        };

        var order = 0;
        foreach (var step in template.Steps)
        {
            workflow.Steps.Add(new WorkflowStep
            {
                Name = step.Name,
                Order = order++,
                RequiredJob = step.Job,
                Instruction = step.Instruction,
                ApprovalPolicy = step.ApprovalPolicy,
                MaxRetries = step.MaxRetries,
            });
        }

        db.Workflows.Add(workflow);
        await db.SaveChangesAsync(ct);

        var created = await LoadWorkflowAsync(workflow.Id, ct);
        return Created($"/api/workflows/{workflow.Id}", ToDto(created!));
    }

    [HttpPut("api/workflows/{id:guid}")]
    public async Task<ActionResult<WorkflowDto>> Update(
        Guid id, [FromBody] UpdateWorkflowRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(detail: "工作流名称必填", statusCode: 400);
        }

        var workflow = await db.Workflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workflow is null)
        {
            return Problem(detail: "工作流不存在", statusCode: 404);
        }

        var error = await ValidateStepsAsync(request.Steps, ct);
        if (error is not null)
        {
            return Problem(detail: error, statusCode: 400);
        }

        workflow.Name = request.Name.Trim();
        workflow.Description = request.Description;
        workflow.UpdatedAt = DateTimeOffset.UtcNow;
        workflow.Steps.Clear();
        ApplySteps(workflow, request.Steps);
        await db.SaveChangesAsync(ct);

        var updated = await LoadWorkflowAsync(id, ct);
        return Ok(ToDto(updated!));
    }

    [HttpDelete("api/workflows/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var workflow = await db.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workflow is null)
        {
            return Problem(detail: "工作流不存在", statusCode: 404);
        }

        db.Workflows.Remove(workflow);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("api/workflows/{id:guid}/runs")]
    public async Task<ActionResult<WorkflowRunDto>> StartRun(
        Guid id, [FromBody] StartWorkflowRequest request, CancellationToken ct)
    {
        try
        {
            var run = await runner.StartAsync(id, request.WorkItemId, ct);
            var dto = await GetRunDtoAsync(run.Id, ct);
            return Created($"/api/workflow-runs/{run.Id}", dto);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: 400);
        }
    }

    [HttpGet("api/workflow-runs")]
    public async Task<ActionResult<IReadOnlyList<WorkflowRunDto>>> ListRuns(
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? workItemId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var query = db.WorkflowRuns.AsNoTracking().AsQueryable();
        if (projectId is Guid pid)
        {
            query = query.Where(r => r.WorkItem.ProjectId == pid);
        }

        if (workItemId is Guid wid)
        {
            query = query.Where(r => r.WorkItemId == wid);
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<WorkflowRunStatus>(status, ignoreCase: true, out var runStatus))
        {
            query = query.Where(r => r.Status == runStatus);
        }

        var rows = await query
            .Select(r => new
            {
                r.Id,
                r.WorkflowId,
                WorkflowName = r.Workflow.Name,
                r.WorkItemId,
                WorkItemTitle = r.WorkItem.Title,
                r.Status,
                r.CurrentStepOrder,
                r.CurrentStepId,
                r.CreatedAt,
                r.FinishedAt,
            })
            .ToListAsync(ct);

        var items = rows
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new WorkflowRunDto(
                r.Id, r.WorkflowId, r.WorkflowName, r.WorkItemId, r.WorkItemTitle,
                r.Status.ToString().ToLowerInvariant(), r.CurrentStepOrder, r.CurrentStepId,
                r.CreatedAt, r.FinishedAt))
            .ToList();
        return Ok(items);
    }

    [HttpGet("api/workflow-runs/{id:guid}")]
    public async Task<ActionResult<WorkflowRunDetailDto>> GetRun(Guid id, CancellationToken ct)
    {
        var run = await GetRunDtoAsync(id, ct);
        if (run is null)
        {
            return Problem(detail: "工作流运行不存在", statusCode: 404);
        }

        var agentRuns = await db.AgentRuns
            .AsNoTracking()
            .Where(r => r.WorkflowRunId == id)
            .Select(r => new
            {
                r.Id,
                AgentName = r.Agent.Name,
                r.WorkItemId,
                WorkItemTitle = r.WorkItem != null ? r.WorkItem.Title : null,
                r.Trigger,
                r.Status,
                r.Error,
                r.CreatedAt,
                r.FinishedAt,
                r.Duration,
                r.PromptTokens,
                r.CompletionTokens,
            })
            .ToListAsync(ct);

        var blockers = await db.Blockers
            .AsNoTracking()
            .Where(b => b.WorkflowRunId == id)
            .Select(b => new
            {
                b.Id,
                b.ProjectId,
                b.WorkItemId,
                WorkItemTitle = b.WorkItem.Title,
                b.WorkflowRunId,
                b.AgentRunId,
                b.Kind,
                b.Reason,
                b.ContextSnapshot,
                b.Status,
                b.Resolution,
                b.ResolutionNote,
                b.CreatedAt,
                b.ResolvedAt,
            })
            .ToListAsync(ct);

        return Ok(new WorkflowRunDetailDto(
            run,
            agentRuns
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new AgentRunListItemDto(
                    r.Id, r.AgentName, r.WorkItemId, r.WorkItemTitle, r.Trigger,
                    r.Status.ToString().ToLowerInvariant(), r.Error, r.CreatedAt, r.FinishedAt,
                    r.Duration, r.PromptTokens, r.CompletionTokens))
                .ToList(),
            blockers
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BlockerDto(
                    b.Id, b.ProjectId, b.WorkItemId, b.WorkItemTitle, b.WorkflowRunId, b.AgentRunId,
                    b.Kind.ToString().ToLowerInvariant(), b.Reason, b.ContextSnapshot,
                    b.Status.ToString().ToLowerInvariant(),
                    b.Resolution?.ToString().ToLowerInvariant(), b.ResolutionNote,
                    b.CreatedAt, b.ResolvedAt))
                .ToList()));
    }

    [HttpPost("api/workflow-runs/{id:guid}/cancel")]
    public async Task<IActionResult> CancelRun(Guid id, CancellationToken ct)
    {
        var canceled = await runner.CancelRunAsync(id, ct);
        return canceled ? NoContent() : Problem(detail: "运行不存在或已结束", statusCode: 400);
    }

    private async Task<WorkflowRunDto?> GetRunDtoAsync(Guid id, CancellationToken ct)
    {
        var row = await db.WorkflowRuns
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.WorkflowId,
                WorkflowName = r.Workflow.Name,
                r.WorkItemId,
                WorkItemTitle = r.WorkItem.Title,
                r.Status,
                r.CurrentStepOrder,
                r.CurrentStepId,
                r.CreatedAt,
                r.FinishedAt,
            })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : new WorkflowRunDto(
                row.Id, row.WorkflowId, row.WorkflowName, row.WorkItemId, row.WorkItemTitle,
                row.Status.ToString().ToLowerInvariant(), row.CurrentStepOrder, row.CurrentStepId,
                row.CreatedAt, row.FinishedAt);
    }

    private async Task<Workflow?> LoadWorkflowAsync(Guid id, CancellationToken ct) =>
        await db.Workflows
            .AsNoTracking()
            .Include(w => w.Steps).ThenInclude(s => s.Agent)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

    private async Task<string?> ValidateStepsAsync(
        IReadOnlyList<WorkflowStepRequest>? steps, CancellationToken ct)
    {
        if (steps is null || steps.Count == 0)
        {
            return "工作流至少需要一个步骤";
        }

        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.Name))
            {
                return "步骤名称必填";
            }

            if (step.AgentId is Guid agentId &&
                !await db.Agents.AnyAsync(a => a.Id == agentId, ct))
            {
                return $"步骤「{step.Name}」指定的 Agent 不存在";
            }

            if (!string.IsNullOrWhiteSpace(step.RequiredJob) &&
                !Enum.TryParse<AgentJob>(step.RequiredJob, ignoreCase: true, out _))
            {
                return $"步骤「{step.Name}」的岗位无效：{step.RequiredJob}";
            }
        }

        return null;
    }

    private static void ApplySteps(Workflow workflow, IReadOnlyList<WorkflowStepRequest> steps)
    {
        var order = 0;
        foreach (var step in steps)
        {
            var effectiveOrder = step.Order ?? order;
            workflow.Steps.Add(new WorkflowStep
            {
                Name = step.Name.Trim(),
                Order = effectiveOrder,
                AgentId = step.AgentId == Guid.Empty ? null : step.AgentId,
                RequiredJob = ParseJob(step.RequiredJob),
                Instruction = step.Instruction,
                ApprovalPolicy = ParsePolicy(step.ApprovalPolicy),
                MaxRetries = Math.Clamp(step.MaxRetries ?? 1, 0, 10),
                InputContract = step.InputContract,
                OutputContract = step.OutputContract,
                Parallel = step.Parallel ?? false,
            });
            order = Math.Max(order, effectiveOrder) + 1;
        }
    }

    private static AgentJob? ParseJob(string? value) =>
        Enum.TryParse<AgentJob>(value, ignoreCase: true, out var job) ? job : null;

    private static ApprovalPolicy ParsePolicy(string? value) =>
        Enum.TryParse<ApprovalPolicy>(value, ignoreCase: true, out var policy)
            ? policy
            : ApprovalPolicy.BlockersOnly;

    private static WorkflowDto ToDto(Workflow workflow) => new(
        workflow.Id,
        workflow.ProjectId,
        workflow.Name,
        workflow.Description,
        workflow.IsTemplate,
        workflow.Enabled,
        workflow.Steps
            .OrderBy(step => step.Order)
            .Select(step => new WorkflowStepDto(
                step.Id,
                step.Order,
                step.Name,
                step.AgentId,
                step.Agent?.Name,
                step.RequiredJob?.ToString().ToLowerInvariant(),
                step.Instruction,
                step.ApprovalPolicy.ToString().ToLowerInvariant(),
                step.MaxRetries,
                step.InputContract,
                step.OutputContract,
                step.Parallel))
            .ToList(),
        workflow.CreatedAt,
        workflow.UpdatedAt);
}
