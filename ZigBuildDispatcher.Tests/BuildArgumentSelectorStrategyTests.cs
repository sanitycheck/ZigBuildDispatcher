namespace ZigBuildDispatcher.Tests;

public sealed class BuildArgumentSelectorStrategyTests
{
    [Fact]
    public void ResolvesFromArgumentPrefix()
    {
        var rules = new[]
        {
            BuildArgumentSelectorRule.FileNamePrefix("-Dartifact=")
        };

        var strategy = new BuildArgumentSelectorStrategy(rules);
        var request = BuildRequest.Create("build.zig", "-Dartifact=plugin.xll");

        var result = strategy.Resolve(request, BuildResult.Empty);

        Assert.True(result.IsSuccess);
        Assert.Equal(ArtifactSelectorKind.FileName, result.Value.Kind);
        Assert.Equal("plugin.xll", result.Value.Value);
    }

    [Fact]
    public void FallsBackToRequestSelectorWhenNoMatch()
    {
        var rules = new[]
        {
            BuildArgumentSelectorRule.FileNamePrefix("-Dartifact=")
        };

        var strategy = new BuildArgumentSelectorStrategy(rules);
        var request = BuildRequest.Create("build.zig", "-Doptimize=ReleaseSmall") with
        {
            ArtifactSelector = BuildArtifactSelector.Extension(".dll")
        };

        var result = strategy.Resolve(request, BuildResult.Empty);

        Assert.True(result.IsSuccess);
        Assert.Equal(ArtifactSelectorKind.Extension, result.Value.Kind);
        Assert.Equal(".dll", result.Value.Value);
    }

    [Fact]
    public void ReturnsFailureWhenValueMissing()
    {
        var rules = new[]
        {
            BuildArgumentSelectorRule.FileNamePrefix("-Dartifact=")
        };

        var strategy = new BuildArgumentSelectorStrategy(rules);
        var request = BuildRequest.Create("build.zig", "-Dartifact=");

        var result = strategy.Resolve(request, BuildResult.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ArtifactSelectorFailed, result.Error.Code);
    }
}
