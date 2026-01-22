namespace ZigBuildDispatcher.Sample;

internal static class SamplePaths
{
    public static bool TryGetSampleBuildZig(out string path)
    {
        path = Path.Combine(AppContext.BaseDirectory, "SampleZigProject", "build.zig");
        if (File.Exists(path))
        {
            return true;
        }

        return TryGetSampleFromSolutionRoot(out path);
    }

    private static bool TryGetSampleFromSolutionRoot(out string path)
    {
        path = string.Empty;
        if (!TryFindSolutionRoot(out var root))
        {
            return false;
        }

        var candidate = Path.Combine(
            root,
            "ZigBuildDispatcher.Sample",
            "SampleZigProject",
            "build.zig");

        if (!File.Exists(candidate))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private static bool TryFindSolutionRoot(out string root)
    {
        root = string.Empty;
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var sln = Path.Combine(current.FullName, "ZigBuildDispatcher.sln");
            if (File.Exists(sln))
            {
                root = current.FullName;
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

}
