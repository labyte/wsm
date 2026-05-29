using System;

namespace WSM.Core.Models;

/// <summary>
/// 操作结果。
/// </summary>
public sealed class OperationResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? ErrorCode { get; set; }

    public Exception? Exception { get; set; }

    public static OperationResult Ok(string? message = null)
    {
        return new OperationResult
        {
            Success = true,
            Message = message ?? string.Empty
        };
    }

    public static OperationResult Fail(string message, Exception? exception = null, string? errorCode = null)
    {
        return new OperationResult
        {
            Success = false,
            Message = message,
            Exception = exception,
            ErrorCode = errorCode
        };
    }
}
