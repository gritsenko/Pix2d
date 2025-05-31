namespace Pix2d.Infrastructure;


public readonly struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    private Result(TValue value)
    {
        IsError = false;
        _value = value;
        _error = default;
    }
    private Result(TError error)
    {
        IsError = true;
        _value = default;
        _error = error;
    }

    public bool IsError { get; }

    public static implicit operator Result<TValue, TError>(TValue value) => new(value);

    public static implicit operator Result<TValue, TError>(TError error) => new(error);

    public TResult Match<TResult>(
        Func<TValue, TResult> success,
        Func<TError, TResult> failure) =>
        !IsError ? success(_value!) : failure(_error!);

    public Task<TResult> MatchAsync<TResult>(
        Func<TValue, Task<TResult>> success,
        Func<TError, Task<TResult>> failure) =>
        !IsError ? success(_value!) : failure(_error!);
    
    public Task MatchAsync(
        Func<TValue, Task> success,
        Func<TError, Task>? failure = default)
    {
        if (!IsError)
            return success(_value!);

        return failure != null ? failure(_error!) : Task.CompletedTask;
    }

    public static Result<TValue, TError> FromNullable(TValue? value, TError error) =>
        value != null ? new Result<TValue, TError>(value) : new Result<TValue, TError>(error);
}