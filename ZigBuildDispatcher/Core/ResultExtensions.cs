namespace ZigBuildDispatcher;

public static class ResultExtensions
{
    public static Result<T> ToFailure<T>(this Result result)
        => Result<T>.Fail(result.Error);

    public static Result<TResult> Map<T, TResult>(this Result<T> result, Func<T, TResult> map)
        => result.IsSuccess ? Result<TResult>.Ok(map(result.Value)) : Result<TResult>.Fail(result.Error);

    public static Result<TResult> Bind<T, TResult>(this Result<T> result, Func<T, Result<TResult>> next)
        => result.IsSuccess ? next(result.Value) : Result<TResult>.Fail(result.Error);
}
