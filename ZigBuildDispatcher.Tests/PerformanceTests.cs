using System.Diagnostics;

namespace ZigBuildDispatcher.Tests;

public sealed class PerformanceTests
{
    [Fact]
    public async Task ConcurrentBuildsCompleteWithinBudget()
    {
        if (!IsEnabled())
        {
            return;
        }

        var root = TestHelpers.CreateTempRoot("perf");
        var projectRoot = Path.Combine(root, "project");
        var buildZig = TestHelpers.CreateBuildFile(projectRoot);
        var fakeZig = TestHelpers.GetFakeZigPath();

        try
        {
            Assert.True(File.Exists(fakeZig), $"Fake zig not found at '{fakeZig}'.");

            const int buildCount = 20;
            const int delayMs = 50;

            var options = new BuildDispatcherOptions
            {
                WorkspaceRoot = Path.Combine(root, "work"),
                MaxConcurrency = 4,
                CleanupWorkspaceOnSuccess = true,
                CleanupWorkspaceOnFailure = true
            };

            var dispatcher = new BuildDispatcher(options);
            var tasks = new List<Task<Result<BuildResult>>>(buildCount);

            for (var i = 0; i < buildCount; i++)
            {
                var artifactName = $"artifact-{i}.bin";
                var request = BuildRequest.Create(
                        buildZig,
                        $"-Dartifact={artifactName}",
                        "-DartifactLocation=bin",
                        $"-DdelayMs={delayMs}")
                    with
                    {
                        ZigHome = fakeZig,
                        ArtifactSelector = BuildArtifactSelector.FileName(artifactName)
                    };

                tasks.Add(dispatcher.DispatchAsync(request).AsTask());
            }

            var stopwatch = Stopwatch.StartNew();
            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            foreach (var result in results)
            {
                Assert.True(result.IsSuccess, result.Error.Message);
            }

            var batches = (int)Math.Ceiling(buildCount / (double)options.MaxConcurrency);
            var expectedMax = TimeSpan.FromMilliseconds((delayMs * batches) + 2000);

            Assert.True(stopwatch.Elapsed < expectedMax,
                $"Elapsed {stopwatch.Elapsed} exceeded budget {expectedMax}.");
        }
        finally
        {
            TestHelpers.CleanupDirectory(root);
        }
    }

    private static bool IsEnabled()
        => string.Equals(Environment.GetEnvironmentVariable("RUN_PERF_TESTS"), "1", StringComparison.Ordinal);
}
