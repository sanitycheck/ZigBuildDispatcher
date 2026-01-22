namespace ZigBuildDispatcher.Tests;

internal static class TestHelpers
{
    private static readonly Lazy<string> SolutionRoot = new(FindSolutionRoot);

    public static string CreateTempRoot(string prefix)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "zig-build-dispatcher-tests",
            prefix,
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    public static string CreateBuildFile(string root)
    {
        Directory.CreateDirectory(root);
        var buildZig = Path.Combine(root, "build.zig");
        File.WriteAllText(buildZig, "// fake build");
        return buildZig;
    }

    public static string GetFakeZigPath()
    {
        var root = SolutionRoot.Value;
        var config = GetConfiguration();
        var exeName = OperatingSystem.IsWindows()
            ? "ZigBuildDispatcher.FakeZig.exe"
            : "ZigBuildDispatcher.FakeZig";

        return Path.Combine(
            root,
            "ZigBuildDispatcher.FakeZig",
            "bin",
            config,
            "net10.0",
            exeName);
    }

    public static void CleanupDirectory(string path)
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

    private static string GetConfiguration()
    {
        var baseDir = AppContext.BaseDirectory;
        return baseDir.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";
    }

    private static string FindSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var sln = Path.Combine(current.FullName, "ZigBuildDispatcher.sln");
            if (File.Exists(sln))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
