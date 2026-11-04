using System.Text.Json;
using CrewPilot.Api.Contracts;
using CrewPilot.Core.Abstractions;
using CrewPilot.Core.Collaboration;
using CrewPilot.Core.Integrations;
using CrewPilot.Core.Projects;
using CrewPilot.Infrastructure.Cicd;
using CrewPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrewPilot.Api.Controllers;

/// <summary>项目 CI/CD 集成：Token 加密存储、连通性测试、PR/MR 创建与状态同步、发布。</summary>
[ApiController]
public sealed class ProjectIntegrationsController(
    CrewPilotDbContext db,
    ISecretProtector secrets,
    IGitCliService gitCli) : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/integration")]
    public async Task<ActionResult<ProjectIntegrationDto>> Get(Guid projectId, CancellationToken ct)
    {
        var integration = await db.ProjectIntegrations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProjectId == projectId, ct);

        return integration is null
            ? Problem(detail: "项目尚未配置 CI/CD 集成", statusCode: 404)
            : Ok(ToDto(integration));
    }

    [HttpPut("api/projects/{projectId:guid}/integration")]
    public async Task<ActionResult<ProjectIntegrationDto>> Upsert(
        Guid projectId, [FromBody] UpsertProjectIntegrationRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<IntegrationPlatform>(request.Platform, ignoreCase: true, out var platform))
        {
            return Problem(detail: "平台无效（github/gitlab）", statusCode: 400);
        }

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
        {
            return Problem(detail: "项目不存在", statusCode: 404);
        }

        var integration = await db.ProjectIntegrations
            .FirstOrDefaultAsync(i => i.ProjectId == projectId, ct);

        if (integration is null)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return Problem(detail: "首次配置必须提供 Token", statusCode: 400);
            }

            integration = new ProjectIntegration
            {
                ProjectId = projectId,
                Platform = platform,
                TokenProtected = secrets.Protect(request.Token.Trim()),
            };
            db.ProjectIntegrations.Add(integration);
        }
        else
        {
            integration.Platform = platform;
            integration.UpdatedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(request.Token))
            {
                integration.TokenProtected = secrets.Protect(request.Token.Trim());
            }
        }

        integration.Repository = string.IsNullOrWhiteSpace(request.Repository)
            ? null
            : request.Repository.Trim();

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(integration));
    }

    [HttpDelete("api/projects/{projectId:guid}/integration")]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken ct)
    {
        var integration = await db.ProjectIntegrations
            .FirstOrDefaultAsync(i => i.ProjectId == projectId, ct);
        if (integration is null)
        {
            return Problem(detail: "项目尚未配置 CI/CD 集成", statusCode: 404);
        }

        db.ProjectIntegrations.Remove(integration);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("api/projects/{projectId:guid}/integration/test")]
    public async Task<ActionResult<TestIntegrationResponse>> Test(Guid projectId, CancellationToken ct)
    {
        var (integration, token, error) = await LoadIntegrationAsync(projectId, ct);
        if (error is not null)
        {
            return error;
        }

        var (ok, message) = await gitCli.TestAsync(integration!.Platform, token!, ct);
        return Ok(new TestIntegrationResponse(ok, message));
    }

    [HttpPost("api/projects/{projectId:guid}/releases")]
    public async Task<ActionResult<ReleaseDto>> CreateRelease(
        Guid projectId, [FromBody] CreateReleaseRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Tag))
        {
            return Problem(detail: "Tag 必填", statusCode: 400);
        }

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        var workingDirectory = project?.LocalPath;
        if (project is null || string.IsNullOrWhiteSpace(workingDirectory) ||
            !Directory.Exists(workingDirectory))
        {
            return Problem(detail: "项目未配置可用的本地工作目录", statusCode: 400);
        }

        var (integration, token, error) = await LoadIntegrationAsync(projectId, ct);
        if (error is not null)
        {
            return error;
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? request.Tag.Trim() : request.Title.Trim();
        var (url, message) = await gitCli.CreateReleaseAsync(
            integration!.Platform, workingDirectory, token!, request.Tag.Trim(), title,
            request.Notes ?? string.Empty, ct);

        if (url is null)
        {
            return Problem(detail: message, statusCode: 400);
        }

        db.Activities.Add(new Activity
        {
            ProjectId = projectId,
            EventType = "release.created",
            Payload = JsonSerializer.Serialize(new { tag = request.Tag, url }),
            ActorKind = MemberKind.User,
        });
        await db.SaveChangesAsync(ct);

        return Ok(new ReleaseDto(request.Tag.Trim(), url, message));
    }

    internal static ProjectIntegrationDto ToDto(ProjectIntegration integration) => new(
        integration.Id,
        integration.ProjectId,
        integration.Platform.ToString().ToLowerInvariant(),
        !string.IsNullOrWhiteSpace(integration.TokenProtected),
        integration.Repository,
        integration.UpdatedAt);

    private async Task<(ProjectIntegration? Integration, string? Token, ActionResult? Error)>
        LoadIntegrationAsync(Guid projectId, CancellationToken ct)
    {
        var integration = await db.ProjectIntegrations
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProjectId == projectId, ct);
        if (integration is null)
        {
            return (null, null, Problem(detail: "项目尚未配置 CI/CD 集成", statusCode: 404));
        }

        try
        {
            var token = secrets.Unprotect(integration.TokenProtected);
            return (integration, token, null);
        }
        catch (Exception)
        {
            return (null, null, Problem(detail: "Token 解密失败，请重新配置", statusCode: 400));
        }
    }
}
