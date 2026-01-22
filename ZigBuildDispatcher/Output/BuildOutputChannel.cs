using System.Threading.Channels;

namespace ZigBuildDispatcher;

public sealed record BuildOutputChannelOptions
{
    public int Capacity { get; init; } = 1024;
    public BoundedChannelFullMode FullMode { get; init; } = BoundedChannelFullMode.DropOldest;
    public bool SingleReader { get; init; } = false;
    public bool SingleWriter { get; init; } = false;
}

public sealed class BuildOutputChannel : IBuildOutputSubscriber, IBuildOutputSubscriberCompletion
{
    private readonly Channel<BuildOutputLine> _channel;
    private readonly ChannelWriter<BuildOutputLine> _writer;

    public BuildOutputChannel()
        : this(new BuildOutputChannelOptions())
    {
    }

    public BuildOutputChannel(BuildOutputChannelOptions options)
    {
        var capacity = Math.Max(1, options.Capacity);
        var channelOptions = new BoundedChannelOptions(capacity)
        {
            FullMode = options.FullMode,
            SingleReader = options.SingleReader,
            SingleWriter = options.SingleWriter
        };

        _channel = Channel.CreateBounded<BuildOutputLine>(channelOptions);
        _writer = _channel.Writer;
    }

    public ChannelReader<BuildOutputLine> Reader => _channel.Reader;

    public IAsyncEnumerable<BuildOutputLine> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    public void OnOutput(BuildOutputLine line)
    {
        _writer.TryWrite(line);
    }

    public void Complete()
    {
        _writer.TryComplete();
    }
}
