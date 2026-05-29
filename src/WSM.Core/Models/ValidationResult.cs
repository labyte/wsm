using System.Collections.Generic;

namespace WSM.Core.Models;

/// <summary>
/// 字段校验错误。
/// </summary>
public sealed class ValidationError
{
    public string Field { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 配置校验结果。
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public IList<ValidationError> Errors { get; } = new List<ValidationError>();

    public static ValidationResult Success() => new ValidationResult();

    public void AddError(string field, string message)
    {
        Errors.Add(new ValidationError { Field = field, Message = message });
    }
}
