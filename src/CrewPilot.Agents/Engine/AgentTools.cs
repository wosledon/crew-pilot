using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CrewPilot.Agents.Abstractions;
using Microsoft.Extensions.AI;

namespace CrewPilot.Agents.Engine;

/// <summary>
/// Agent 内置工具：read_file / write_file / list_files / run_command。
/// 全部限制在工作目录内；run_command 仅执行白名单命令，不经过 shell，带超时与输出截断
/// （见 AGENTS.md 第 6 节）。
/// </summary>
public static class AgentTools
{
    public const int CommandTimeoutMs = 120_000;

    public const int MaxOutputChars = 8_000;

    /// <summary>引擎支持的全部工具名（与 Agent.Tools 字段的 JSON 数组对齐）。</summary>
    public static IReadOnlyList<string> AllToolNames { get; } =
        ["read_file", "write_file", "list_files", "run_command"];

    /// <summary>
    /// 创建可用的工具集合。
    /// <paramref name="enabledTools"/> 为空表示全部启用；否则只启用其中列出的工具。
    /// </summary>
    public static IList<AITool> Create(
        string? workingDirectory,
        IReadOnlyList<string>? enabledTools = null,
        IReadOnlyList<string>? whitelist = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            return [];
        }

        var root = Path.GetFullPath(workingDirectory);
        var enabled = enabledTools is { Count: > 0 }
            ? enabledTools.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : AllToolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tools = new List<AITool>();

        if (enabled.Contains("read_file"))
        {
            tools.Add(AIFunctionFactory.Create(
                ([Description("相对工作目录的文件路径")] string path) =>
                {
                    var full = ResolvePath(root, path);
                    return full is null || !File.Exists(full)
                        ? $"文件不存在或越权：{path}"
                        : File.ReadAllText(full);
                },
                "read_file",
                "读取工作目录中的文件内容"));
        }

        if (enabled.Contains("write_file"))
        {
            tools.Add(AIFunctionFactory.Create(
                ([Description("相对工作目录的文件路径")] string path,
                 [Description("要写入的完整内容")] string content) =>
                {
                    var full = ResolvePath(root, path);
                    if (full is null || full == root)
                    {
                        return $"非法路径：{path}";
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                    File.WriteAllText(full, content);
                    return $"已写入 {path}（{content.Length} 字符）";
                },
                "write_file",
                "把内容写入工作目录中的文件"));
        }

        if (enabled.Contains("list_files"))
        {
            tools.Add(AIFunctionFactory.Create(
                ([Description("相对工作目录的目录路径，留空为根目录")] string path) =>
                {
                    var full = ResolvePath(root, string.IsNullOrWhiteSpace(path) ? "." : path);
                    if (full is null || !Directory.Exists(full))
                    {
                        return $"目录不存在或越权：{path}";
                    }

                    return string.Join('\n', Directory.GetFileSystemEntries(full)
                        .Select(entry => Path.GetRelativePath(root, entry))
                        .OrderBy(entry => entry, StringComparer.Ordinal));
                },
                "list_files",
                "列出工作目录中的文件与子目录"));
        }

        if (enabled.Contains("run_command") && whitelist is { Count: > 0 })
        {
            var allowed = whitelist.ToHashSet(StringComparer.OrdinalIgnoreCase);
            tools.Add(AIFunctionFactory.Create(
                ([Description("要执行的命令，如 git status（不经过 shell，不支持管道/重定向）")] string command) =>
                {
                    var tokens = TokenizeCommand(command);
                    if (tokens.Count == 0)
                    {
                        return "空命令";
                    }

                    var name = Path.GetFileNameWithoutExtension(tokens[0]);
                    if (!allowed.Contains(name))
                    {
                        return $"命令不在白名单：{name}";
                    }

                    return RunCommandAsync(root, tokens, cancellationToken).GetAwaiter().GetResult();
                },
                "run_command",
                "在工作目录执行白名单内的终端命令（git/dotnet/npm/gh 等）"));
        }

        return tools;
    }

    /// <summary>
    /// 把 MCP 工具包装为 MAF 可调用的函数。MCP 的 inputSchema 作为描述附注，
    /// 模型通过单个 JSON 字符串参数传参。
    /// </summary>
    public static IList<AITool> CreateMcpTools(
        IReadOnlyList<McpToolDescriptor>? descriptors,
        IMcpToolInvoker? invoker)
    {
        if (descriptors is null || invoker is null || descriptors.Count == 0)
        {
            return [];
        }

        var tools = new List<AITool>();
        foreach (var descriptor in descriptors)
        {
            var description = BuildMcpDescription(descriptor);
            Func<string, CancellationToken, Task<string>> call = (
                [Description("该 MCP 工具的参数 JSON 对象字符串（需符合 inputSchema）")] string arguments,
                CancellationToken cancellationToken) =>
                invoker.CallToolAsync(descriptor.Name, arguments, cancellationToken);
            tools.Add(AIFunctionFactory.Create(call, descriptor.Name, description));
        }

        return tools;
    }

    private static string BuildMcpDescription(McpToolDescriptor descriptor)
    {
        var description = string.IsNullOrWhiteSpace(descriptor.Description)
            ? $"MCP 工具 {descriptor.Name}"
            : descriptor.Description;
        return string.IsNullOrWhiteSpace(descriptor.InputSchemaJson)
            ? description
            : $"{description}\n参数 inputSchema：{descriptor.InputSchemaJson}";
    }

    /// <summary>把相对路径解析到工作目录内；越界返回 null（含同前缀兄弟目录逃逸）。</summary>
    public static string? ResolvePath(string workingDirectory, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var root = Path.GetFullPath(workingDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            return full;
        }

        var rootWithSeparator = root + Path.DirectorySeparatorChar;
        return full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    /// <summary>把命令行拆成参数（支持双引号/单引号，不经过 shell）。</summary>
    public static IReadOnlyList<string> TokenizeCommand(string command)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inDoubleQuote = false;
        var inSingleQuote = false;

        foreach (var ch in command)
        {
            if (ch == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
            }
            else if (ch == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
            }
            else if (char.IsWhiteSpace(ch) && !inDoubleQuote && !inSingleQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static async Task<string> RunCommandAsync(
        string workingDirectory,
        IReadOnlyList<string> tokens,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(tokens[0])
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in tokens.Skip(1))
        {
            psi.ArgumentList.Add(argument);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(CommandTimeoutMs);

        using var process = new Process { StartInfo = psi };
        Task<string>? stdoutTask = null;
        Task<string>? stderrTask = null;
        try
        {
            process.Start();
            stdoutTask = process.StandardOutput.ReadToEndAsync();
            stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(timeoutCts.Token);

            var text = (await stdoutTask) + (await stderrTask);
            return text.Length > MaxOutputChars
                ? text[..MaxOutputChars] + "\n…（输出已截断）"
                : text;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await DrainAsync(stdoutTask, stderrTask);
            return cancellationToken.IsCancellationRequested
                ? "命令已取消"
                : $"命令超时（{CommandTimeoutMs}ms）：{tokens[0]}";
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 进程可能已退出
        }
    }

    private static async Task DrainAsync(params Task<string>?[] tasks)
    {
        try
        {
            await Task.WhenAll(tasks.OfType<Task<string>>()).WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 已 kill，读取任务可能以异常结束，忽略即可
        }
    }
}
