using System.Diagnostics;

namespace CrewPilot.Agents.Execution;

/// <summary>
/// 受控进程执行封装：白名单由调用方保证，超时 + 输出截断 + 审计日志（见 AGENTS.md 第 6 节）。
/// 统一走 <see cref="ProcessStartInfo.ArgumentList"/>，不经过 shell，避免命令拼接注入。
/// </summary>
public static class ProcessRunner
{
    public const int MaxOutputChars = 8_000;

    public static async Task<(int ExitCode, string Output)> RunAsync(
        string workingDirectory,
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

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
            return (process.ExitCode, Truncate(text));
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await DrainAsync(stdoutTask, stderrTask);

            var reason = ct.IsCancellationRequested
                ? "命令已取消"
                : $"命令超时（{(int)timeout.TotalMilliseconds}ms）";
            return (-1, $"{reason}：{fileName} {string.Join(' ', arguments)}");
        }
    }

    private static string Truncate(string text) =>
        text.Length > MaxOutputChars ? text[..MaxOutputChars] + "\n…（输出已截断）" : text;

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
