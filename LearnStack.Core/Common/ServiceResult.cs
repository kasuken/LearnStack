namespace LearnStack.Common;

/// <summary>
/// Represents the outcome of a void service operation that may fail due to
/// authorization or business rule violations.
/// </summary>
public readonly record struct ServiceResult(bool Success, string? Error = null)
{
    public static ServiceResult Ok() => new(true);
    public static ServiceResult Fail(string error) => new(false, error);
}

/// <summary>
/// Represents the outcome of a service operation that returns a value.
/// </summary>
public readonly record struct ServiceResult<T>(bool Success, T? Value, string? Error = null)
{
    public static ServiceResult<T> Ok(T value) => new(true, value);
    public static ServiceResult<T> Fail(string error) => new(false, default, error);
}
