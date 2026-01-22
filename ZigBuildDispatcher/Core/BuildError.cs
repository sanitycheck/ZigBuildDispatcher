namespace ZigBuildDispatcher;

public sealed record BuildError(string Code, string Message, BuildResult Output)
{
    public static BuildError None { get; } = new(ErrorCodes.None, string.Empty, BuildResult.Empty);

    public static BuildError WithoutOutput(string code, string message)
        => new(code, message, BuildResult.Empty);

    public static BuildError WithOutput(string code, string message, BuildResult output)
        => new(code, message, output);

    public static BuildError BuildFailed(BuildResult output)
        => new(ErrorCodes.BuildFailed, "Zig build failed.", output);

    public static BuildError Cancelled(BuildResult output)
        => new(ErrorCodes.Cancelled, "Build cancelled.", output);
}
