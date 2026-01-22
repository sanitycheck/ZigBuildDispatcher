namespace ZigBuildDispatcher.Tests;

public sealed class BuildArtifactReaderTests
{
    [Fact]
    public void AutoDetectPrefersBinOverOut()
    {
        var root = CreateRoot();

        try
        {
            var workspace = CreateWorkspace(root, false, string.Empty);
            var binDir = Path.Combine(workspace.OutDir, "bin");
            Directory.CreateDirectory(binDir);

            var binFile = Path.Combine(binDir, "app.exe");
            var outFile = Path.Combine(workspace.OutDir, "other.dll");

            File.WriteAllText(binFile, "bin");
            File.WriteAllText(outFile, "out");

            var reader = new BuildArtifactReader();
            var request = BuildRequest.Create("build.zig") with { ArtifactSelector = BuildArtifactSelector.Auto };

            var result = reader.Read(workspace, request);

            Assert.True(result.IsSuccess);
            Assert.Equal(binFile, result.Value.Path);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void AutoDetectIgnoresDebugSidecars()
    {
        var root = CreateRoot();

        try
        {
            var workspace = CreateWorkspace(root, false, string.Empty);
            var binDir = Path.Combine(workspace.OutDir, "bin");
            Directory.CreateDirectory(binDir);

            var exeFile = Path.Combine(binDir, "app.exe");
            var pdbFile = Path.Combine(binDir, "app.pdb");

            File.WriteAllText(exeFile, "exe");
            File.WriteAllText(pdbFile, "pdb");

            var reader = new BuildArtifactReader();
            var request = BuildRequest.Create("build.zig") with { ArtifactSelector = BuildArtifactSelector.Auto };

            var result = reader.Read(workspace, request);

            Assert.True(result.IsSuccess);
            Assert.Equal(exeFile, result.Value.Path);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void AutoDetectPrefersDllOverImportLib()
    {
        var root = CreateRoot();

        try
        {
            var workspace = CreateWorkspace(root, false, string.Empty);
            var binDir = Path.Combine(workspace.OutDir, "bin");
            Directory.CreateDirectory(binDir);

            var dllFile = Path.Combine(binDir, "plugin.dll");
            var libFile = Path.Combine(binDir, "plugin.lib");
            var pdbFile = Path.Combine(binDir, "plugin.pdb");

            File.WriteAllText(dllFile, "dll");
            File.WriteAllText(libFile, "lib");
            File.WriteAllText(pdbFile, "pdb");

            var reader = new BuildArtifactReader();
            var request = BuildRequest.Create("build.zig") with { ArtifactSelector = BuildArtifactSelector.Auto };

            var result = reader.Read(workspace, request);

            Assert.True(result.IsSuccess);
            Assert.Equal(dllFile, result.Value.Path);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void ExtensionSelectorFindsMatchingFile()
    {
        var root = CreateRoot();

        try
        {
            var workspace = CreateWorkspace(root, false, string.Empty);
            var binDir = Path.Combine(workspace.OutDir, "bin");
            Directory.CreateDirectory(binDir);

            var exeFile = Path.Combine(binDir, "app.exe");
            var dllFile = Path.Combine(binDir, "lib.dll");

            File.WriteAllText(exeFile, "exe");
            File.WriteAllText(dllFile, "dll");

            var reader = new BuildArtifactReader();
            var request = BuildRequest.Create("build.zig") with
            {
                ArtifactSelector = BuildArtifactSelector.Extension(".dll")
            };

            var result = reader.Read(workspace, request);

            Assert.True(result.IsSuccess);
            Assert.Equal(dllFile, result.Value.Path);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void PatternSelectorFindsSingleMatch()
    {
        var root = CreateRoot();

        try
        {
            var workspace = CreateWorkspace(root, false, string.Empty);
            var binDir = Path.Combine(workspace.OutDir, "bin");
            Directory.CreateDirectory(binDir);

            var outFile = Path.Combine(workspace.OutDir, "plugin.xll");
            var binFile = Path.Combine(binDir, "ignore.exe");

            File.WriteAllText(outFile, "xll");
            File.WriteAllText(binFile, "exe");

            var reader = new BuildArtifactReader();
            var request = BuildRequest.Create("build.zig") with
            {
                ArtifactSelector = BuildArtifactSelector.Pattern("*.xll", ArtifactSearchScope.OutOnly)
            };

            var result = reader.Read(workspace, request);

            Assert.True(result.IsSuccess);
            Assert.Equal(outFile, result.Value.Path);
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

    private static BuildWorkspace CreateWorkspace(
        string root,
        bool sharedGlobalCache,
        string sharedGlobalCacheDir)
    {
        var cacheDir = Path.Combine(root, "zig-cache");
        var outDir = Path.Combine(root, "zig-out");
        var tempDir = Path.Combine(root, "zig-tmp");
        var globalCacheDir = sharedGlobalCache ? sharedGlobalCacheDir : Path.Combine(root, "zig-global-cache");

        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(globalCacheDir);

        return new BuildWorkspace(
            root,
            cacheDir,
            globalCacheDir,
            outDir,
            tempDir,
            sharedGlobalCache);
    }

    private static void Cleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
        catch
        {
        }
    }
}
