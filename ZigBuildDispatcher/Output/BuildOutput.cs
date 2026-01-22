namespace ZigBuildDispatcher;

public enum BuildOutputKind
{
    StandardOutput,
    StandardError
}

public readonly record struct BuildOutputLine(BuildOutputKind Kind, string Text);

public interface IBuildOutputSubscriber
{
    void OnOutput(BuildOutputLine line);
}

public interface IBuildOutputSubscriberCompletion
{
    void Complete();
}

public sealed class BuildOutputSubscriber : IBuildOutputSubscriber, IBuildOutputSubscriberCompletion
{
    public static BuildOutputSubscriber None { get; } = new();

    private BuildOutputSubscriber()
    {
    }

    public void OnOutput(BuildOutputLine line)
    {
    }

    public void Complete()
    {
    }
}
