namespace ZigBuildDispatcher.Tests;

public sealed class RealZigIntegrationTests
{
    [Fact]
    public async Task BuildsWithRealZigWhenAvailable()
    {
        if (!TryGetZigHome(out var zigHome))
        {
            return;
        }

        var root = TestHelpers.CreateTempRoot("real-zig");
        var projectRoot = Path.Combine(root, "project");

        try
        {
            var buildZig = CreateZigProject(projectRoot);
            var options = new BuildDispatcherOptions
            {
                WorkspaceRoot = Path.Combine(root, "work"),
                CleanupWorkspaceOnSuccess = false,
                CleanupWorkspaceOnFailure = false
            };

            var dispatcher = new BuildDispatcher(options);
            var artifactName = OperatingSystem.IsWindows() ? "hello.exe" : "hello";
            var request = BuildRequest.Create(buildZig, "-Doptimize=ReleaseSmall") with
            {
                ZigHome = zigHome,
                ArtifactSelector = BuildArtifactSelector.FileName(artifactName)
            };

            var result = await dispatcher.DispatchAsync(request);

            Assert.True(result.IsSuccess, result.Error.Message);
            Assert.NotEmpty(result.Value.Artifact.Bytes);

            var workspace = result.Value.Workspace;
            Assert.False(Directory.Exists(workspace.CacheDir));
            Assert.False(Directory.Exists(workspace.OutDir));
            Assert.False(Directory.Exists(workspace.TempDir));
        }
        finally
        {
            TestHelpers.CleanupDirectory(root);
        }
    }

    private static bool TryGetZigHome(out string zigHome)
    {
        zigHome = Environment.GetEnvironmentVariable("ZIG_EXE") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(zigHome))
        {
            zigHome = Environment.GetEnvironmentVariable("ZIG_HOME") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(zigHome))
        {
            return false;
        }

        return File.Exists(zigHome) || Directory.Exists(zigHome);
    }

    private static string CreateZigProject(string root)
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "src"));

        var buildZig = Path.Combine(root, "build.zig");
        var mainZig = Path.Combine(root, "src", "main.zig");

        File.WriteAllText(buildZig, ZigBuildTemplate);
        File.WriteAllText(mainZig, ZigMainTemplate);

        return buildZig;
    }

    private const string ZigBuildTemplate = """
        const std = @import("std");

        pub fn build(b: *std.Build) void {
            const target = b.standardTargetOptions(.{});
            const optimize = b.standardOptimizeOption(.{});

            const root_module = b.createModule(.{
                .root_source_file = b.path("src/main.zig"),
                .target = target,
                .optimize = optimize,
            });

            const exe = b.addExecutable(.{
                .name = "hello",
                .root_module = root_module,
            });
            b.installArtifact(exe);
        }
        """;

    private const string ZigMainTemplate = """
        const std = @import("std");

        pub fn main() void {
            std.debug.print("hello", .{});
        }
        """;
}
