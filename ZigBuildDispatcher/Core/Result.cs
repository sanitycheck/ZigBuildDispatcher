namespace ZigBuildDispatcher;

public readonly struct Result
{
    public bool IsSuccess { get; }
    public BuildError Error { get; }

    private Result(bool isSuccess, BuildError error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Ok() => new(true, BuildError.None);
    public static Result Fail(BuildError error) => new(false, error);
}

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public BuildError Error { get; }

    private Result(bool isSuccess, T value, BuildError error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(true, value, BuildError.None);
    public static Result<T> Fail(BuildError error) => new(false, default!, error);
}
