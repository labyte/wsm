namespace WSM.Infrastructure.WinSw;

/// <summary>
/// WinSW CLI 命令执行结果。
/// </summary>
public sealed class WinSwCommandResult
{
    public int ExitCode { get; set; }

    public string StandardOutput { get; set; } = string.Empty;

    public string StandardError { get; set; } = string.Empty;

    public bool Success => ExitCode == 0;
}
