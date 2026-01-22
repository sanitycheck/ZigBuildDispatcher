namespace ZigBuildDispatcher;

public sealed record BuildDispatcherOptions
{
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;
    public string WorkspaceRoot { get; init; } = Path.Combine(Path.GetTempPath(), "zig-build-dispatcher");
    public string SharedGlobalCacheDir { get; init; } = string.Empty;
    public int MaxOutputChars { get; init; } = 256 * 1024;
    public BuildArtifactCleanupMode ArtifactCleanupMode { get; init; } = BuildArtifactCleanupMode.All;
    public bool CleanupWorkspaceOnSuccess { get; init; } = true;
    public bool CleanupWorkspaceOnFailure { get; init; } = false;
}
