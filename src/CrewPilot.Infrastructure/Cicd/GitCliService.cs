using System.Text.Json;
using System.Text.RegularExpressions;
using CrewPilot.Core.Integrations;

namespace CrewPilot.Infrastructure.Cicd;

/// <summary>
/// gh / glab CLI 实现：统一注入 Token 环境变量，不经过 shell，带超时与输出截断。
/// GitHub 使用 GH_TOKEN；GitLab 使用 GITLAB_TOKEN。
/// </summary>
public sealed class GitCliService : IGitCliService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    private static readonly Regex GitHubPrUrlPattern = new(
        @"https?://\S+/pull/\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GitLabMrUrlPattern = new(
        @"https?://\S+/-/merge_requests/\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<(bool Ok, string Message)> TestAsync(
        IntegrationPlatform platform, string token, CancellationToken ct = default)
    {
        var (fileName, arguments) = platform switch
        {
            IntegrationPlatform.GitHub => ("gh", new[] { "auth", "status" }),
            _ => ("glab", new[] { "auth", "status" }),
        };

        var (exitCode, output) = await RunAsync(
            Directory.GetCurrentDirectory(), platform, token, fileName, arguments, ct);

        if (exitCode == 0)
        {
            return (true, "CLI 认证成功");
        }

        return (false, $"CLI 认证失败（退出码 {exitCode}）：{Shorten(output)}");
    }

    public async Task<(string? Url, string Message)> CreatePullRequestAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string title,
        string body,
        string headBranch,
        CancellationToken ct = default)
    {
        string fileName;
        string[] arguments;
        Regex urlPattern;

        if (platform == IntegrationPlatform.GitHub)
        {
            fileName = "gh";
            arguments =
            [
                "pr", "create",
                "--title", title,
                "--body", body,
                "--head", headBranch,
            ];
            urlPattern = GitHubPrUrlPattern;
        }
        else
        {
            fileName = "glab";
            arguments =
            [
                "mr", "create",
                "--title", title,
                "--description", body,
                "--source-branch", headBranch,
                "--yes",
            ];
            urlPattern = GitLabMrUrlPattern;
        }

        var (exitCode, output) = await RunAsync(
            workingDirectory, platform, token, fileName, arguments, ct);
        var match = urlPattern.Match(output);
        if (exitCode == 0 && match.Success)
        {
            return (match.Value, "PR/MR 已创建");
        }

        return (null, $"创建 PR/MR 失败（退出码 {exitCode}）：{Shorten(output)}");
    }

    public async Task<PullRequestStatus?> GetPullRequestAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string pullRequestUrl,
        CancellationToken ct = default)
    {
        try
        {
            return platform == IntegrationPlatform.GitHub
                ? await GetGitHubPullRequestAsync(workingDirectory, token, pullRequestUrl, ct)
                : await GetGitLabMergeRequestAsync(workingDirectory, token, pullRequestUrl, ct);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<(bool Ok, string Message)> MergePullRequestAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string pullRequestUrl,
        CancellationToken ct = default)
    {
        var (fileName, arguments) = platform == IntegrationPlatform.GitHub
            ? ("gh", new[] { "pr", "merge", pullRequestUrl, "--merge" })
            : ("glab", new[] { "mr", "merge", pullRequestUrl, "--yes" });

        var (exitCode, output) = await RunAsync(
            workingDirectory, platform, token, fileName, arguments, ct);
        return exitCode == 0
            ? (true, "PR/MR 已合并")
            : (false, $"合并失败（退出码 {exitCode}）：{Shorten(output)}");
    }

    public async Task<(string? Url, string Message)> CreateReleaseAsync(
        IntegrationPlatform platform,
        string workingDirectory,
        string token,
        string tag,
        string title,
        string notes,
        CancellationToken ct = default)
    {
        var (fileName, arguments) = platform == IntegrationPlatform.GitHub
            ? ("gh", new[] { "release", "create", tag, "--title", title, "--notes", notes })
            : ("glab", new[] { "release", "create", tag, "--name", title, "--notes", notes });

        var (exitCode, output) = await RunAsync(
            workingDirectory, platform, token, fileName, arguments, ct);
        if (exitCode != 0)
        {
            return (null, $"创建 Release 失败（退出码 {exitCode}）：{Shorten(output)}");
        }

        var match = Regex.Match(output, @"https?://\S+", RegexOptions.IgnoreCase);
        return (match.Success ? match.Value : null, "Release 已创建");
    }

    private async Task<PullRequestStatus?> GetGitHubPullRequestAsync(
        string workingDirectory, string token, string pullRequestUrl, CancellationToken ct)
    {
        var (exitCode, output) = await RunAsync(
            workingDirectory, IntegrationPlatform.GitHub, token, "gh",
            ["pr", "view", pullRequestUrl, "--json", "state,mergeable,statusCheckRollup,url"], ct);
        if (exitCode != 0)
        {
            return null;
        }

        using var document = JsonDocument.Parse(ExtractJson(output));
        var root = document.RootElement;
        var state = root.TryGetProperty("state", out var stateElement)
            ? stateElement.GetString() ?? "UNKNOWN"
            : "UNKNOWN";
        bool? mergeable = root.TryGetProperty("mergeable", out var mergeableElement)
            ? string.Equals(mergeableElement.GetString(), "MERGEABLE", StringComparison.OrdinalIgnoreCase)
            : null;
        var url = root.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;

        var checks = new List<CiCheck>();
        if (root.TryGetProperty("statusCheckRollup", out var rollup) &&
            rollup.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in rollup.EnumerateArray())
            {
                var name = GetString(item, "name") ?? GetString(item, "context") ?? "check";
                var status = GetString(item, "status") ?? GetString(item, "state") ?? "unknown";
                var conclusion = GetString(item, "conclusion") ?? GetString(item, "state");
                var detailsUrl = GetString(item, "detailsUrl") ?? GetString(item, "targetUrl");
                checks.Add(new CiCheck(name, status, conclusion, detailsUrl));
            }
        }

        return new PullRequestStatus(state, mergeable, checks, url);
    }

    private async Task<PullRequestStatus?> GetGitLabMergeRequestAsync(
        string workingDirectory, string token, string pullRequestUrl, CancellationToken ct)
    {
        var parsed = ParseGitLabMergeRequestUrl(pullRequestUrl);
        if (parsed is null)
        {
            return null;
        }

        var (projectPath, iid) = parsed.Value;
        var apiPath = $"projects/{Uri.EscapeDataString(projectPath)}/merge_requests/{iid}";
        var (exitCode, output) = await RunAsync(
            workingDirectory, IntegrationPlatform.GitLab, token, "glab",
            ["api", apiPath], ct);
        if (exitCode != 0)
        {
            return null;
        }

        using var document = JsonDocument.Parse(ExtractJson(output));
        var root = document.RootElement;
        var state = root.TryGetProperty("state", out var stateElement)
            ? stateElement.GetString() ?? "unknown"
            : "unknown";
        bool? mergeable = root.TryGetProperty("merge_status", out var mergeStatus)
            ? string.Equals(mergeStatus.GetString(), "can_be_merged", StringComparison.OrdinalIgnoreCase)
            : null;
        var url = root.TryGetProperty("web_url", out var urlElement) ? urlElement.GetString() : null;

        var checks = new List<CiCheck>();
        if (root.TryGetProperty("head_pipeline", out var pipeline) &&
            pipeline.ValueKind == JsonValueKind.Object)
        {
            var pipelineStatus = GetString(pipeline, "status") ?? "pending";
            var pipelineUrl = GetString(pipeline, "web_url");
            checks.Add(new CiCheck("pipeline", pipelineStatus, pipelineStatus, pipelineUrl));
        }

        return new PullRequestStatus(state, mergeable, checks, url);
    }

    private static (string ProjectPath, int Iid)? ParseGitLabMergeRequestUrl(string url)
    {
        var match = Regex.Match(
            url,
            @"https?://[^/]+/(?<path>.+?)/-/merge_requests/(?<iid>\d+)",
            RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups["iid"].Value, out var iid))
        {
            return null;
        }

        return (match.Groups["path"].Value, iid);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string workingDirectory,
        IntegrationPlatform platform,
        string token,
        string fileName,
        string[] arguments,
        CancellationToken ct)
    {
        var environment = new Dictionary<string, string?>
        {
            ["NO_COLOR"] = "1",
            ["GH_PROMPT_DISABLED"] = "1",
        };
        if (platform == IntegrationPlatform.GitHub)
        {
            environment["GH_TOKEN"] = token;
        }
        else
        {
            environment["GITLAB_TOKEN"] = token;
        }

        try
        {
            return await Execution.ProcessRunner.RunAsync(
                workingDirectory, fileName, arguments, Timeout, ct, environment);
        }
        catch (Exception ex)
        {
            return (-1, $"CLI 不可用（{fileName}）：{ex.Message}");
        }
    }

    private static string ExtractJson(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        return start >= 0 && end > start ? output[start..(end + 1)] : "{}";
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Shorten(string output) =>
        output.Length > 500 ? output[..500] : output;
}
