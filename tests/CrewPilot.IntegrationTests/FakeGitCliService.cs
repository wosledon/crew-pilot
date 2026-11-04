using CrewPilot.Core.Integrations;
using CrewPilot.Infrastructure.Cicd;

namespace CrewPilot.IntegrationTests;

/// <summary>测试用 CI/CD CLI 假实现：不调用真实 gh/glab。</summary>
public sealed class FakeGitCliService : IGitCliService
{
    public bool Available { get; set; } = true;

    public string PullRequestUrl { get; set; } =
        "https://github.com/example/crew-pilot/pull/77";

    public string ReleaseUrl { get; set; } =
        "https://github.com/example/crew-pilot/releases/tag/v9.9.9";

    public PullRequestStatus Status { get; set; } = new(
        "OPEN",
        true,
        [new CiCheck("build", "completed", "success", "https://ci.example.com/1")],
        "https://github.com/example/crew-pilot/pull/77");

    public int CreatePullRequestCalls { get; private set; }

    public int GetStatusCalls { get; private set; }

    public int CreateReleaseCalls { get; private set; }

    public int MergeCalls { get; private set; }

    public Task<(bool Ok, string Message)> TestAsync(
        IntegrationPlatform platform, string token, CancellationToken ct = default)
    {
        return Task.FromResult(Available
            ? (true, "CLI 认证成功")
            : (false, "fake cli 不可用"));
    }

    public Task<(string? Url, string Message)> CreatePullRequestAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string title,
        string body,
        string headBranch,
        CancellationToken ct = default)
    {
        CreatePullRequestCalls++;
        return Task.FromResult<(string?, string)>(
            Available ? (PullRequestUrl, "PR/MR 已创建") : (null, "fake cli 不可用"));
    }

    public Task<PullRequestStatus?> GetPullRequestAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string pullRequestUrl,
        CancellationToken ct = default)
    {
        GetStatusCalls++;
        return Task.FromResult<PullRequestStatus?>(Available ? Status : null);
    }

    public Task<(bool Ok, string Message)> MergePullRequestAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string pullRequestUrl,
        CancellationToken ct = default)
    {
        MergeCalls++;
        return Task.FromResult(Available ? (true, "PR/MR 已合并") : (false, "fake cli 不可用"));
    }

    public Task<(string? Url, string Message)> CreateReleaseAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string tag,
        string title,
        string notes,
        CancellationToken ct = default)
    {
        CreateReleaseCalls++;
        return Task.FromResult<(string?, string)>(
            Available ? (ReleaseUrl, "Release 已创建") : (null, "fake cli 不可用"));
    }
}
