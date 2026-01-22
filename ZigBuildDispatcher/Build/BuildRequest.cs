namespace ZigBuildDispatcher;

public sealed record BuildRequest(string BuildZigPath, IReadOnlyList<string> BuildArguments)
{
    public string ZigHome { get; init; } = string.Empty;
    public BuildArtifactSelector ArtifactSelector { get; init; } = BuildArtifactSelector.Auto;
    public IBuildOutputSubscriber OutputSubscriber { get; init; } = BuildOutputSubscriber.None;
    public string CacheKey { get; init; } = string.Empty;
    public string WorkspaceKey { get; init; } = string.Empty;
    public BuildArtifactCleanupMode ArtifactCleanupMode { get; init; } = BuildArtifactCleanupMode.Default;
    public WorkspaceCleanupMode WorkspaceCleanupMode { get; init; } = WorkspaceCleanupMode.Default;

    public static BuildRequest Create(string buildZigPath, params string[] buildArguments)
        => new(buildZigPath, buildArguments);

    public static BuildRequest Create(
        string buildZigPath,
        BuildArtifactSelector selector,
        params string[] buildArguments)
        => new(buildZigPath, buildArguments) { ArtifactSelector = selector };
}
