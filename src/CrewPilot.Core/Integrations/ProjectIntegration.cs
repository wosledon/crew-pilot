namespace CrewPilot.Core.Integrations;

using CrewPilot.Core.Common;

/// <summary>CI/CD 平台。</summary>
public enum IntegrationPlatform
{
    GitHub,
    GitLab,
}

/// <summary>项目 CI/CD 集成配置：Token 加密存储，永不出现在响应与日志。</summary>
public class ProjectIntegration : Entity
{
    public Guid ProjectId { get; set; }

    public Projects.Project Project { get; set; } = null!;

    public IntegrationPlatform Platform { get; set; }

    /// <summary>加密后的访问 Token（gh: GH_TOKEN / glab: GITLAB_TOKEN）。</summary>
    public string TokenProtected { get; set; } = string.Empty;

    /// <summary>覆盖项目 repoUrl 的仓库标识（可选，如 owner/repo）。</summary>
    public string? Repository { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
