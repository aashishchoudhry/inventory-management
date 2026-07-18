namespace InventoryErp.Application.Common;

/// <summary>
/// Outcome of an application service call. Services return this instead of throwing for
/// expected failures (not found, duplicate, invalid input); exceptions remain reserved for
/// genuinely unexpected faults.
/// </summary>
public class ServiceResult
{
    protected ServiceResult(ResultStatus status, string? error, IReadOnlyList<string>? validationErrors)
    {
        Status = status;
        Error = error;
        ValidationErrors = validationErrors ?? [];
    }

    public ResultStatus Status { get; }

    public bool IsSuccess => Status == ResultStatus.Success;

    /// <summary>Human-readable failure message. Null on success.</summary>
    public string? Error { get; }

    /// <summary>Field-level messages, populated for <see cref="ResultStatus.ValidationFailed"/>.</summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    public static ServiceResult Success() => new(ResultStatus.Success, null, null);

    public static ServiceResult NotFound(string message = "The requested item was not found.")
        => new(ResultStatus.NotFound, message, null);

    public static ServiceResult Invalid(params string[] errors)
        => new(ResultStatus.ValidationFailed, "One or more validation errors occurred.", errors);

    public static ServiceResult Invalid(IReadOnlyList<string> errors)
        => new(ResultStatus.ValidationFailed, "One or more validation errors occurred.", errors);

    public static ServiceResult Conflict(string message)
        => new(ResultStatus.Conflict, message, null);

    public static ServiceResult Unauthorized(string message = "You are not allowed to perform this action.")
        => new(ResultStatus.Unauthorized, message, null);

    public static ServiceResult Failure(string message)
        => new(ResultStatus.Error, message, null);
}

/// <inheritdoc cref="ServiceResult"/>
/// <typeparam name="T">Payload carried on success.</typeparam>
public sealed class ServiceResult<T> : ServiceResult
{
    private ServiceResult(ResultStatus status, T? data, string? error, IReadOnlyList<string>? validationErrors)
        : base(status, error, validationErrors)
    {
        Data = data;
    }

    /// <summary>Payload. Non-null when <see cref="ServiceResult.IsSuccess"/> is true.</summary>
    public T? Data { get; }

    public static ServiceResult<T> Success(T data)
        => new(ResultStatus.Success, data, null, null);

    public new static ServiceResult<T> NotFound(string message = "The requested item was not found.")
        => new(ResultStatus.NotFound, default, message, null);

    public new static ServiceResult<T> Invalid(params string[] errors)
        => new(ResultStatus.ValidationFailed, default, "One or more validation errors occurred.", errors);

    public new static ServiceResult<T> Invalid(IReadOnlyList<string> errors)
        => new(ResultStatus.ValidationFailed, default, "One or more validation errors occurred.", errors);

    public new static ServiceResult<T> Conflict(string message)
        => new(ResultStatus.Conflict, default, message, null);

    public new static ServiceResult<T> Unauthorized(string message = "You are not allowed to perform this action.")
        => new(ResultStatus.Unauthorized, default, message, null);

    public new static ServiceResult<T> Failure(string message)
        => new(ResultStatus.Error, default, message, null);

    /// <summary>Lets a service <c>return dto;</c> directly on the success path.</summary>
    public static implicit operator ServiceResult<T>(T data) => Success(data);
}
