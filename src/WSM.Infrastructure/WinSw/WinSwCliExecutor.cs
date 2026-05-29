using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WSM.Infrastructure.Logging;

namespace WSM.Infrastructure.WinSw;

/// <summary>
/// 封装 WinSW 命令行调用。
/// </summary>
public sealed class WinSwCliExecutor
{
    /// <summary>
    /// 执行 WinSW 命令。包装器 exe 与 xml 需同名同目录。
    /// </summary>
    public async Task<WinSwCommandResult> ExecuteAsync(
        string wrapperExePath,
        string command,
        CancellationToken cancellationToken = default,
        Action<string>? onOutputLine = null)
    {
        if (string.IsNullOrWhiteSpace(wrapperExePath))
        {
            throw new ArgumentException("包装器路径不能为空。", nameof(wrapperExePath));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("命令不能为空。", nameof(command));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = wrapperExePath,
            Arguments = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = LogTextEncodingHelper.GetProcessOutputEncoding(),
            StandardErrorEncoding = LogTextEncodingHelper.GetProcessOutputEncoding()
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        if (!process.Start())
        {
            return new WinSwCommandResult
            {
                ExitCode = -1,
                StandardError = "无法启动 WinSW 进程。"
            };
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using (cancellationToken.Register(() =>
               {
                   try
                   {
                       if (!process.HasExited)
                       {
                           process.Kill();
                       }
                   }
                   catch
                   {
                       // 忽略取消时的进程清理异常
                   }
               }))
        {
            await Task.Run(() => process.WaitForExit(), cancellationToken).ConfigureAwait(false);
        }

        var stdout = (await stdoutTask.ConfigureAwait(false)).Trim();
        var stderr = (await stderrTask.ConfigureAwait(false)).Trim();

        EmitLines(onOutputLine, stdout);
        EmitLines(onOutputLine, stderr, prefix: "[stderr] ");

        return new WinSwCommandResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            StandardError = stderr
        };
    }

    /// <summary>
    /// 解析 status 命令输出。
    /// </summary>
    public static string ParseStatusOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return "Unknown";
        }

        var line = output.Trim();
        var firstLine = line.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
        return firstLine.Trim();
    }

    private static void EmitLines(Action<string>? onOutputLine, string text, string? prefix = null)
    {
        if (onOutputLine == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            onOutputLine(prefix + line);
        }
    }
}
