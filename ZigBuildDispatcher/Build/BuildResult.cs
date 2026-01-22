namespace ZigBuildDispatcher;

public sealed record BuildResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool StandardOutputTruncated,
    bool StandardErrorTruncated,
    TimeSpan Duration,
    BuildWorkspace Workspace,
    BuildArtifact Artifact)
{
    public bool Succeeded => ExitCode == 0;

    public static BuildResult Empty { get; } = new(
        -1,
        string.Empty,
        string.Empty,
        false,
        false,
        TimeSpan.Zero,
        BuildWorkspace.Empty,
        BuildArtifact.Empty);
}
