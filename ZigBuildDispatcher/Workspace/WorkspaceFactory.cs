using System.Security.Cryptography;
using System.Text;

namespace ZigBuildDispatcher;

public interface IWorkspaceFactory
{
    Result<BuildWorkspace> Create(BuildRequest request);
    Result CleanupArtifacts(BuildWorkspace workspace, BuildArtifactCleanupMode mode);
    Result Cleanup(BuildWorkspace workspace);
}

public sealed class WorkspaceFactory : IWorkspaceFactory
{
    private readonly BuildDispatcherOptions _options;

    public WorkspaceFactory(BuildDispatcherOptions options)
    {
        _options = options;
    }

    public Result<BuildWorkspace> Create(BuildRequest request)
    {
        var rootBase = string.IsNullOrWhiteSpace(_options.WorkspaceRoot)
            ? Path.Combine(Path.GetTempPath(), "zig-build-dispatcher")
            : _options.WorkspaceRoot;

        var cacheKey = BuildKeyHasher.ToPathSegment(request.CacheKey);
        var root = string.IsNullOrWhiteSpace(cacheKey)
            ? rootBase
            : Path.Combine(rootBase, cacheKey);

        var workspaceKey = BuildKeyHasher.ToPathSegment(request.WorkspaceKey);
        var buildRoot = string.IsNullOrWhiteSpace(workspaceKey)
            ? Path.Combine(root, Guid.NewGuid().ToString("N"))
            : Path.Combine(root, workspaceKey);
        var cacheDir = Path.Combine(buildRoot, "zig-cache");
        var outDir = Path.Combine(buildRoot, "zig-out");
        var tempDir = Path.Combine(buildRoot, "zig-tmp");
        var isGlobalCacheShared = !string.IsNullOrWhiteSpace(_options.SharedGlobalCacheDir);
        var globalCacheDir = isGlobalCacheShared
            ? ResolveSharedGlobalCacheDir(_options.SharedGlobalCacheDir, cacheKey)
            : Path.Combine(buildRoot, "zig-global-cache");

        try
        {
            Directory.CreateDirectory(buildRoot);
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(tempDir);

            Directory.CreateDirectory(globalCacheDir);

            var workspace = new BuildWorkspace(
                buildRoot,
                cacheDir,
                globalCacheDir,
                outDir,
                tempDir,
                isGlobalCacheShared);
            return Result<BuildWorkspace>.Ok(workspace);
        }
        catch (Exception ex)
        {
            return Result<BuildWorkspace>.Fail(BuildError.WithoutOutput(
                ErrorCodes.WorkspaceCreateFailed,
                $"Workspace creation failed: {ex.Message}"));
        }
    }

    public Result Cleanup(BuildWorkspace workspace)
    {
        return DeleteDirectory(workspace.Root);
    }

    public Result CleanupArtifacts(BuildWorkspace workspace, BuildArtifactCleanupMode mode)
    {
        var resolved = mode == BuildArtifactCleanupMode.Default
            ? BuildArtifactCleanupMode.All
            : mode;

        return resolved switch
        {
            BuildArtifactCleanupMode.None => Result.Ok(),
            BuildArtifactCleanupMode.OutputOnly => CleanupOutput(workspace),
            _ => CleanupAll(workspace)
        };
    }

    private static Result DeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return Result.Ok();
        }

        try
        {
            Directory.Delete(path, true);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(BuildError.WithoutOutput(
                ErrorCodes.CleanupFailed,
                $"Workspace cleanup failed: {ex.Message}"));
        }
    }

    private static string ResolveSharedGlobalCacheDir(string baseDir, string cacheKey)
        => string.IsNullOrWhiteSpace(cacheKey)
            ? baseDir
            : Path.Combine(baseDir, cacheKey);

    private static Result CleanupOutput(BuildWorkspace workspace)
    {
        var outResult = DeleteDirectory(workspace.OutDir);
        if (!outResult.IsSuccess)
        {
            return outResult;
        }

        return DeleteDirectory(workspace.TempDir);
    }

    private static Result CleanupAll(BuildWorkspace workspace)
    {
        var cacheResult = DeleteDirectory(workspace.CacheDir);
        if (!cacheResult.IsSuccess)
        {
            return cacheResult;
        }

        var outResult = DeleteDirectory(workspace.OutDir);
        if (!outResult.IsSuccess)
        {
            return outResult;
        }

        var tempResult = DeleteDirectory(workspace.TempDir);
        if (!tempResult.IsSuccess)
        {
            return tempResult;
        }

        return workspace.IsGlobalCacheShared
            ? Result.Ok()
            : DeleteDirectory(workspace.GlobalCacheDir);
    }

    private static class BuildKeyHasher
    {
        public static string ToPathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
