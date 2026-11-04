using System.Text.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Integrations;
using CrewPilot.Core.Projects;
using CrewPilot.Core.Services;
using CrewPilot.Core.WorkItems;
using CrewPilot.Infrastructure.Cicd;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>工作项 PR/MR：创建、状态与 CI 同步回写。</summary>
[ApiController]
public sealed class PullRequestsController(
    CrewPilotDbContext db,
    ISecretProtector secrets,
    IGitCliService gitCli) : ControllerBase
{
    [HttpPost("api/workitems/{workItemId:guid}/pull-request")]
    public async Task<ActionResult<PullRequestCreatedDto>> Create(
        Guid workItemId, [FromBody] CreatePullRequestRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Branch) || string.IsNullOrWhiteSpace(request.Title))
        {
            return Problem(detail: "分支与标题必填", statusCode: 400);
        }

        var context = await LoadContextAsync(workItemId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        var body = string.IsNullOrWhiteSpace(request.Body)
            ? $"工作项：{context.WorkItem!.Title}"
            : request.Body;
        var (url, message) = await gitCli.CreatePullRequestAsync(
            context.Integration!.Platform,
            context.WorkingDirectory!,
            context.Token!,
            request.Title.Trim(),
            body,
            request.Branch.Trim(),
            ct);

        if (url is null)
        {
            return Problem(detail: message, statusCode: 400);
        }

        context.WorkItem!.PrUrl = url;
        context.WorkItem.UpdatedAt = DateTimeOffset.UtcNow;
        db.Activities.Add(new Activity
        {
            ProjectId = context.WorkItem.ProjectId,
            WorkItemId = context.WorkItem.Id,
            EventType = "pullrequest.created",
            Payload = JsonSerializer.Serialize(new { url, branch = request.Branch }),
            ActorKind = MemberKind.Agent,
        });
        await db.SaveChangesAsync(ct);

        return Created(url, new PullRequestCreatedDto(url));
    }

    [HttpGet("api/workitems/{workItemId:guid}/pull-request")]
    public async Task<ActionResult<PullRequestStatusDto>> Get(
        Guid workItemId, CancellationToken ct)
    {
        var context = await LoadContextAsync(workItemId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        if (string.IsNullOrWhiteSpace(context.WorkItem!.PrUrl))
        {
            return Problem(detail: "工作项尚未关联 PR/MR", statusCode: 400);
        }

        var status = await gitCli.GetPullRequestAsync(
            context.Integration!.Platform,
            context.WorkingDirectory!,
            context.Token!,
            context.WorkItem.PrUrl,
            ct);

        return status is null
            ? Problem(detail: "无法获取 PR/MR 状态（CLI 不可用或 PR 不存在）", statusCode: 400)
            : Ok(ToDto(status));
    }

    [HttpPost("api/workitems/{workItemId:guid}/pull-request/sync")]
    public async Task<ActionResult<PullRequestStatusDto>> Sync(
        Guid workItemId, CancellationToken ct)
    {
        var context = await LoadContextAsync(workItemId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        if (string.IsNullOrWhiteSpace(context.WorkItem!.PrUrl))
        {
            return Problem(detail: "工作项尚未关联 PR/MR", statusCode: 400);
        }

        var status = await gitCli.GetPullRequestAsync(
            context.Integration!.Platform,
            context.WorkingDirectory!,
            context.Token!,
            context.WorkItem.PrUrl,
            ct);
        if (status is null)
        {
            return Problem(detail: "无法获取 PR/MR 状态（CLI 不可用或 PR 不存在）", statusCode: 400);
        }

        context.WorkItem.PrState = status.NormalizedState;
        context.WorkItem.CiStatus = status.NormalizedCiStatus;
        context.WorkItem.UpdatedAt = DateTimeOffset.UtcNow;

        // PR 合并后自动推进工作项状态（需求 → 已完成；缺陷 → 已关闭）
        if (status.NormalizedState == "merged")
        {
            TryAdvanceMerged(context.WorkItem);
        }
        db.Activities.Add(new Activity
        {
            ProjectId = context.WorkItem.ProjectId,
            WorkItemId = context.WorkItem.Id,
            EventType = "pullrequest.synced",
            Payload = JsonSerializer.Serialize(new
            {
                state = context.WorkItem.PrState,
                ci = context.WorkItem.CiStatus,
            }),
            ActorKind = MemberKind.User,
        });
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(status));
    }

    private async Task<(
        WorkItem? WorkItem,
        ProjectIntegration? Integration,
        string? Token,
        string? WorkingDirectory,
        ActionResult? Error)> LoadContextAsync(Guid workItemId, CancellationToken ct)
    {
        var workItem = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct);
        if (workItem is null)
        {
            return (null, null, null, null, Problem(detail: "工作项不存在", statusCode: 404));
        }

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == workItem.ProjectId, ct);
        if (project is null || string.IsNullOrWhiteSpace(project.LocalPath) ||
            !Directory.Exists(project.LocalPath))
        {
            return (null, null, null, null,
                Problem(detail: "项目未配置可用的本地工作目录", statusCode: 400));
        }

        var integration = await db.ProjectIntegrations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProjectId == workItem.ProjectId, ct);
        if (integration is null)
        {
            return (null, null, null, null,
                Problem(detail: "项目尚未配置 CI/CD 集成", statusCode: 404));
        }

        try
        {
            var token = secrets.Unprotect(integration.TokenProtected);
            return (workItem, integration, token, project.LocalPath, null);
        }
        catch (Exception)
        {
            return (null, null, null, null,
                Problem(detail: "Token 解密失败，请重新配置", statusCode: 400));
        }
    }

    private static PullRequestStatusDto ToDto(PullRequestStatus status) => new(
        status.NormalizedState,
        status.NormalizedCiStatus,
        status.Mergeable,
        status.Url,
        status.Checks
            .Select(check => new CiCheckDto(
                check.Name, check.Status, check.Conclusion, check.Url))
            .ToList());

    private static void TryAdvanceMerged(WorkItem workItem)
    {
        try
        {
            switch (workItem)
            {
                case Requirement requirement when requirement.Status == RequirementStatus.InReview:
                    WorkItemStateMachine.Transition(requirement, RequirementStatus.Done);
                    break;
                case Defect defect when defect.Status == DefectStatus.PendingVerification:
                    WorkItemStateMachine.Transition(defect, DefectStatus.Closed);
                    break;
            }
        }
        catch (InvalidOperationException)
        {
            // 状态机不允许时忽略（保留人工处理）
        }
    }
}
