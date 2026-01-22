namespace ZigBuildDispatcher;

public sealed record BuildWorkspace(
    string Root,
    string CacheDir,
    string GlobalCacheDir,
    string OutDir,
    string TempDir,
    bool IsGlobalCacheShared)
{
    public static BuildWorkspace Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        false);
}
