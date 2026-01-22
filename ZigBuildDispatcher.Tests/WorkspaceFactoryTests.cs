namespace ZigBuildDispatcher.Tests;

public sealed class WorkspaceFactoryTests
{
    [Fact]
    public void CleanupArtifactsPreservesSharedGlobalCache()
    {
        var root = CreateRoot();
        var sharedGlobalCache = Path.Combine(
            Path.GetTempPath(),
            "zig-build-dispatcher-tests",
            Guid.NewGuid().ToString("N"),
            "global-cache");

        try
        {
            var options = new BuildDispatcherOptions
            {
                WorkspaceRoot = root,
                SharedGlobalCacheDir = sharedGlobalCache,
                CleanupWorkspaceOnSuccess = false,
                CleanupWorkspaceOnFailure = false
            };

            var factory = new WorkspaceFactory(options);
            var workspaceResult = factory.Create(BuildRequest.Create("build.zig"));

            Assert.True(workspaceResult.IsSuccess);

            var workspace = workspaceResult.Value;
            File.WriteAllText(Path.Combine(workspace.CacheDir, "cache.txt"), "cache");
            File.WriteAllText(Path.Combine(workspace.OutDir, "out.txt"), "out");
            File.WriteAllText(Path.Combine(workspace.TempDir, "tmp.txt"), "tmp");

            var cleanupResult = factory.CleanupArtifacts(workspace, BuildArtifactCleanupMode.All);

            Assert.True(cleanupResult.IsSuccess);
            Assert.False(Directory.Exists(workspace.CacheDir));
            Assert.False(Directory.Exists(workspace.OutDir));
            Assert.False(Directory.Exists(workspace.TempDir));
            Assert.True(Directory.Exists(sharedGlobalCache));
        }
        finally
        {
            Cleanup(sharedGlobalCache);
            Cleanup(root);
        }
    }

    [Fact]
    public void CleanupArtifactsRemovesLocalGlobalCache()
    {
        var root = CreateRoot();

        try
        {
            var options = new BuildDispatcherOptions
            {
                WorkspaceRoot = root,
                CleanupWorkspaceOnSuccess = false,
                CleanupWorkspaceOnFailure = false
            };

            var factory = new WorkspaceFactory(options);
            var workspaceResult = factory.Create(BuildRequest.Create("build.zig"));

            Assert.True(workspaceResult.IsSuccess);

            var workspace = workspaceResult.Value;
            File.WriteAllText(Path.Combine(workspace.GlobalCacheDir, "cache.txt"), "cache");

            var cleanupResult = factory.CleanupArtifacts(workspace, BuildArtifactCleanupMode.All);

            Assert.True(cleanupResult.IsSuccess);
            Assert.False(Directory.Exists(workspace.GlobalCacheDir));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CleanupArtifactsOutputOnlyPreservesCaches()
    {
        var root = CreateRoot();

        try
        {
            var options = new BuildDispatcherOptions
            {
                WorkspaceRoot = root,
                CleanupWorkspaceOnSuccess = false,
                CleanupWorkspaceOnFailure = false
            };

            var factory = new WorkspaceFactory(options);
            var workspaceResult = factory.Create(BuildRequest.Create("build.zig"));

            Assert.True(workspaceResult.IsSuccess);

            var workspace = workspaceResult.Value;
            File.WriteAllText(Path.Combine(workspace.CacheDir, "cache.txt"), "cache");
            File.WriteAllText(Path.Combine(workspace.OutDir, "out.txt"), "out");
            File.WriteAllText(Path.Combine(workspace.TempDir, "tmp.txt"), "tmp");
            File.WriteAllText(Path.Combine(workspace.GlobalCacheDir, "global.txt"), "global");

            var cleanupResult = factory.CleanupArtifacts(workspace, BuildArtifactCleanupMode.OutputOnly);

            Assert.True(cleanupResult.IsSuccess);
            Assert.True(Directory.Exists(workspace.CacheDir));
            Assert.False(Directory.Exists(workspace.OutDir));
            Assert.False(Directory.Exists(workspace.TempDir));
            Assert.True(Directory.Exists(workspace.GlobalCacheDir));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void CleanupArtifactsNoneLeavesDirectories()
    {
        var root = CreateRoot();

        try
        {
            var options = new BuildDispatcherOptions
            {
                WorkspaceRoot = root,
                CleanupWorkspaceOnSuccess = false,
                CleanupWorkspaceOnFailure = false
            };

            var factory = new WorkspaceFactory(options);
            var workspaceResult = factory.Create(BuildRequest.Create("build.zig"));

            Assert.True(workspaceResult.IsSuccess);

            var workspace = workspaceResult.Value;
            File.WriteAllText(Path.Combine(workspace.CacheDir, "cache.txt"), "cache");
            File.WriteAllText(Path.Combine(workspace.OutDir, "out.txt"), "out");
            File.WriteAllText(Path.Combine(workspace.TempDir, "tmp.txt"), "tmp");

            var cleanupResult = factory.CleanupArtifacts(workspace, BuildArtifactCleanupMode.None);

            Assert.True(cleanupResult.IsSuccess);
            Assert.True(Directory.Exists(workspace.CacheDir));
            Assert.True(Directory.Exists(workspace.OutDir));
            Assert.True(Directory.Exists(workspace.TempDir));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void WorkspaceKeyReusesRoot()
    {
        var root = CreateRoot();

        try
        {
            var options = new BuildDispatcherOptions
            {
                WorkspaceRoot = root,
                CleanupWorkspaceOnSuccess = false,
                CleanupWorkspaceOnFailure = false
            };

            var factory = new WorkspaceFactory(options);
            var request = BuildRequest.Create("build.zig") with
            {
                WorkspaceKey = "session-1"
            };

            var first = factory.Create(request);
            var second = factory.Create(request);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.Equal(first.Value.Root, second.Value.Root);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "zig-build-dispatcher-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    private static void Cleanup(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }
}
