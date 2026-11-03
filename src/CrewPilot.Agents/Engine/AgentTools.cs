using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.AI;

namespace CrewPilot.Agents.Engine;

/// <summary>
/// Agent 内置工具：read_file / write_file / list_files / run_command。
/// 全部限制在工作目录内；run_command 仅执行白名单命令，带超时与输出截断（见 AGENTS.md 第 6 节）。
/// </summary>
public static class AgentTools
{
    public const int CommandTimeoutMs = 120_000;

    public const int MaxOutputChars = 8_000;

    public static IList<AITool> Create(string? workingDirectory, IReadOnlyList<string>? whitelist)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            return [];
        }

        var root = Path.GetFullPath(workingDirectory);
        var tools = new List<AITool>();

        string? Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var full = Path.GetFullPath(Path.Combine(root, path));
            return full.StartsWith(root, StringComparison.Ordinal) ? full : null;
        }

        tools.Add(AIFunctionFactory.Create(
            ([Description("相对工作目录的文件路径")] string path) =>
            {
                var full = Resolve(path);
                return full is null || !File.Exists(full)
                    ? $"文件不存在或越权：{path}"
                    : File.ReadAllText(full);
            },
            "read_file",
            "读取工作目录中的文件内容"));

        tools.Add(AIFunctionFactory.Create(
            ([Description("相对工作目录的文件路径")] string path,
             [Description("要写入的完整内容")] string content) =>
            {
                var full = Resolve(path);
                if (full is null)
                {
                    return $"非法路径：{path}";
                }

                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, content);
                return $"已写入 {path}（{content.Length} 字符）";
            },
            "write_file",
            "把内容写入工作目录中的文件"));

        tools.Add(AIFunctionFactory.Create(
            ([Description("相对工作目录的目录路径，留空为根目录")] string path) =>
            {
                var full = Resolve(string.IsNullOrWhiteSpace(path) ? "." : path);
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

        if (whitelist is { Count: > 0 })
        {
            var allowed = whitelist.ToHashSet(StringComparer.OrdinalIgnoreCase);
            tools.Add(AIFunctionFactory.Create(
                ([Description("要执行的命令，如 git status")] string command) =>
                {
                    var executable = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                    var name = Path.GetFileNameWithoutExtension(executable);
                    if (!allowed.Contains(name))
                    {
                        return $"命令不在白名单：{name}";
                    }

                    return RunCommand(root, command);
                },
                "run_command",
                "在工作目录执行白名单内的终端命令（git/dotnet/npm/gh 等）"));
        }

        return tools;
    }

    private static string RunCommand(string workingDirectory, string command)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c {command}")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return "命令启动失败";
        }

        var output = new StringBuilder();
        output.AppendLine(process.StandardOutput.ReadToEnd());
        output.AppendLine(process.StandardError.ReadToEnd());

        if (!process.WaitForExit(CommandTimeoutMs))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 进程可能已退出
            }

            return $"命令超时（{CommandTimeoutMs}ms）：{command}";
        }

        var text = output.ToString();
        return text.Length > MaxOutputChars ? text[..MaxOutputChars] + "\n…（输出已截断）" : text;
    }
}
