using CrewPilot.Core.Integrations;

namespace CrewPilot.Infrastructure.Cicd;

/// <summary>CI/CD 平台 CLI（gh / glab）的受控封装。所有调用均带 Token 环境变量、超时与输出截断。</summary>
public interface IGitCliService
{
    /// <summary>CLI 是否可用（安装且可执行）。</summary>
    Task<(bool Ok, string Message)> TestAsync(
        IntegrationPlatform platform, string token, CancellationToken ct = default);

    /// <summary>创建 PR/MR，返回可访问 URL（失败返回 null + 消息）。</summary>
    Task<(string? Url, string Message)> CreatePullRequestAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string title,
        string body,
        string headBranch,
        CancellationToken ct = default);

    /// <summary>查询 PR/MR 状态与 CI 检查。</summary>
    Task<PullRequestStatus?> GetPullRequestAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string pullRequestUrl,
        CancellationToken ct = default);

    /// <summary>合并 PR/MR。</summary>
    Task<(bool Ok, string Message)> MergePullRequestAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string pullRequestUrl,
        CancellationToken ct = default);

    /// <summary>创建 Release（打 tag）。</summary>
    Task<(string? Url, string Message)> CreateReleaseAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string tag,
        string title,
        string notes,
        CancellationToken ct = default);
}

public sealed record CiCheck(string Name, string Status, string? Conclusion, string? Url);

public sealed record PullRequestStatus(
    string State,
    bool? Mergeable,
    IReadOnlyList<CiCheck> Checks,
    string? Url)
{
    /// <summary>把 PR 状态归一化为 open / merged / closed。</summary>
    public string NormalizedState => State.ToLowerInvariant() switch
    {
        "merged" => "merged",
        "closed" => "closed",
        "open" => "open",
        _ => "unknown",
    };

    /// <summary>把检查结果归一化为 success / failure / pending。</summary>
    public string NormalizedCiStatus
    {
        get
        {
            if (Checks.Count == 0)
            {
                return "none";
            }

            if (Checks.Any(c => IsFailure(c)))
            {
                return "failure";
            }

            if (Checks.Any(c => IsPending(c)))
            {
                return "pending";
            }

            return "success";
        }
    }

    private static bool IsFailure(CiCheck check) =>
        string.Equals(check.Conclusion, "failure", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(check.Conclusion, "timed_out", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(check.Conclusion, "cancelled", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(check.Status, "failed", StringComparison.OrdinalIgnoreCase);

    private static bool IsPending(CiCheck check) =>
        string.Equals(check.Status, "queued", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(check.Status, "in_progress", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(check.Status, "running", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(check.Status, "pending", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(check.Conclusion);
}
