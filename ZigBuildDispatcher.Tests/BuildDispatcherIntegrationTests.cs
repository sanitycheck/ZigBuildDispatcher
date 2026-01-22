namespace ZigBuildDispatcher.Tests;

public sealed class BuildDispatcherIntegrationTests
{
    [Fact]
    public async Task SuccessfulBuildReturnsArtifactBytesAndCleansArtifacts()
    {
        var root = TestHelpers.CreateTempRoot("success");
        var projectRoot = Path.Combine(root, "project");
        var buildZig = TestHelpers.CreateBuildFile(projectRoot);
        var fakeZig = TestHelpers.GetFakeZigPath();

        try
        {
            Assert.True(File.Exists(fakeZig), $"Fake zig not found at '{fakeZig}'.");

            var options = new BuildDispatcherOptions
            {
                WorkspaceRoot = Path.Combine(root, "work"),
                CleanupWorkspaceOnSuccess = false,
                CleanupWorkspaceOnFailure = false
            };

            var dispatcher = new BuildDispatcher(options);
            var artifactName = OperatingSystem.IsWindows() ? "app.exe" : "app";
            var request = BuildRequest.Create(
                    buildZig,
                    $"-Dartifact={artifactName}",
                    "-DartifactLocation=bin")
                with
                {
                    ZigHome = fakeZig,
                    ArtifactSelector = BuildArtifactSelector.FileName(artifactName)
                };

            var result = await dispatcher.DispatchAsync(request);

            Assert.True(result.IsSuccess, result.Error.Message);
            Assert.Contains(artifactName, System.Text.Encoding.UTF8.GetString(result.Value.Artifact.Bytes));

            var workspace = result.Value.Workspace;
            Assert.False(Directory.Exists(workspace.CacheDir));
            Assert.False(Directory.Exists(workspace.OutDir));
            Assert.False(Directory.Exists(workspace.TempDir));
            Assert.True(Directory.Exists(workspace.Root));
        }
        finally
        {
            TestHelpers.CleanupDirectory(root);
        }
    }

    [Fact]
    public async Task FailedBuildCleansArtifacts()
    {
        var root = TestHelpers.CreateTempRoot("failure");
        var projectRoot = Path.Combine(root, "project");
        var buildZig = TestHelpers.CreateBuildFile(projectRoot);
        var fakeZig = TestHelpers.GetFakeZigPath();

        try
        {
            Assert.True(File.Exists(fakeZig), $"Fake zig not found at '{fakeZig}'.");

            var options = new BuildDispatcherOptions
            {
                WorkspaceRoot = Path.Combine(root, "work"),
                CleanupWorkspaceOnSuccess = false,
                CleanupWorkspaceOnFailure = false
            };

            var dispatcher = new BuildDispatcher(options);
            var artifactName = OperatingSystem.IsWindows() ? "app.exe" : "app";
            var request = BuildRequest.Create(
                    buildZig,
                    $"-Dartifact={artifactName}",
                    "-DartifactLocation=bin",
                    "-Dexit=1")
                with
                {
                    ZigHome = fakeZig,
                    ArtifactSelector = BuildArtifactSelector.FileName(artifactName)
                };

            var result = await dispatcher.DispatchAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.BuildFailed, result.Error.Code);

            var workspace = result.Error.Output.Workspace;
            Assert.False(Directory.Exists(workspace.CacheDir));
            Assert.False(Directory.Exists(workspace.OutDir));
            Assert.False(Directory.Exists(workspace.TempDir));
        }
        finally
        {
            TestHelpers.CleanupDirectory(root);
        }
    }

    [Fact]
    public async Task StreamsOutputToSubscriber()
    {
        var root = TestHelpers.CreateTempRoot("output");
        var projectRoot = Path.Combine(root, "project");
        var buildZig = TestHelpers.CreateBuildFile(projectRoot);
        var fakeZig = TestHelpers.GetFakeZigPath();

        try
        {
            Assert.True(File.Exists(fakeZig), $"Fake zig not found at '{fakeZig}'.");

            var options = new BuildDispatcherOptions
            {
                WorkspaceRoot = Path.Combine(root, "work"),
                CleanupWorkspaceOnSuccess = false,
                CleanupWorkspaceOnFailure = false
            };

            var dispatcher = new BuildDispatcher(options);
            var output = new BuildOutputChannel(new BuildOutputChannelOptions
            {
                Capacity = 16,
                SingleReader = true,
                SingleWriter = true
            });

            var artifactName = OperatingSystem.IsWindows() ? "app.exe" : "app";
            var request = BuildRequest.Create(
                    buildZig,
                    $"-Dartifact={artifactName}",
                    "-DartifactLocation=bin",
                    "-DstdoutLines=2",
                    "-DstderrLines=1")
                with
                {
                    ZigHome = fakeZig,
                    ArtifactSelector = BuildArtifactSelector.FileName(artifactName),
                    OutputSubscriber = output
                };

            var readTask = CollectOutputAsync(output);
            var result = await dispatcher.DispatchAsync(request);
            var lines = await readTask;

            Assert.True(result.IsSuccess, result.Error.Message);
            Assert.Equal(3, lines.Count);
            Assert.Contains(lines, line => line.Text == "stdout-0");
            Assert.Contains(lines, line => line.Text == "stdout-1");
            Assert.Contains(lines, line => line.Text == "stderr-0");
        }
        finally
        {
            TestHelpers.CleanupDirectory(root);
        }
    }

    private static async Task<List<BuildOutputLine>> CollectOutputAsync(BuildOutputChannel output)
    {
        var lines = new List<BuildOutputLine>();
        await foreach (var line in output.ReadAllAsync())
        {
            lines.Add(line);
        }

        return lines;
    }
}
