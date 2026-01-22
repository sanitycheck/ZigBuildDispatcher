using System.Threading.Channels;

namespace ZigBuildDispatcher.Tests;

public sealed class BuildOutputChannelTests
{
    [Fact]
    public async Task CompletesAndEmitsOutput()
    {
        var channel = new BuildOutputChannel(new BuildOutputChannelOptions
        {
            Capacity = 4,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });

        channel.OnOutput(new BuildOutputLine(BuildOutputKind.StandardOutput, "hello"));
        channel.Complete();

        var lines = new List<BuildOutputLine>();
        await foreach (var line in channel.ReadAllAsync())
        {
            lines.Add(line);
        }

        Assert.Single(lines);
        Assert.Equal("hello", lines[0].Text);
    }
}
