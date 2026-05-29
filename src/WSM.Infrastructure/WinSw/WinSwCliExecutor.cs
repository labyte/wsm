using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        CancellationToken cancellationToken = default)
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
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using (var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
        {
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            if (!process.Start())
            {
                return new WinSwCommandResult
                {
                    ExitCode = -1,
                    StandardError = "无法启动 WinSW 进程。"
                };
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

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

            return new WinSwCommandResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = outputBuilder.ToString().Trim(),
                StandardError = errorBuilder.ToString().Trim()
            };
        }
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
}
