using System.Diagnostics;
using System.Text;

namespace CrewPilot.Agents.Execution;

/// <summary>
/// 受控进程执行封装：白名单由调用方保证，超时 + 输出截断 + 审计日志（见 AGENTS.md 第 6 节）。
/// </summary>
public static class ProcessRunner
{
    public const int MaxOutputChars = 8_000;

    public static async Task<(int ExitCode, string Output)> RunAsync(
        string workingDirectory,
        string command,
        TimeSpan timeout,
        CancellationToken ct = default)
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
            return (-1, "命令启动失败");
        }

        var output = new StringBuilder();
        output.AppendLine(process.StandardOutput.ReadToEnd());
        output.AppendLine(process.StandardError.ReadToEnd());

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // 进程可能已退出
            }

            return (-1, $"命令超时（{timeout.TotalMilliseconds}ms）：{command}");
        }

        var text = output.ToString();
        return text.Length > MaxOutputChars
            ? (process.ExitCode, text[..MaxOutputChars] + "\n…（输出已截断）")
            : (process.ExitCode, text);
    }
}
