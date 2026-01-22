namespace ZigBuildDispatcher;

public sealed class BuildDispatcher
{
    private readonly SemaphoreSlim _concurrency;
    private readonly IZigLocator _zigLocator;
    private readonly IWorkspaceFactory _workspaceFactory;
    private readonly IZigProcessRunner _processRunner;
    private readonly IBuildArtifactSelectorStrategy _artifactSelectorStrategy;
    private readonly IBuildArtifactReader _artifactReader;
    private readonly BuildDispatcherOptions _options;

    public BuildDispatcher(BuildDispatcherOptions options)
        : this(
            options,
            new DefaultZigLocator(),
            new WorkspaceFactory(options),
            new ZigProcessRunner(options),
            new RequestArtifactSelectorStrategy(),
            new BuildArtifactReader())
    {
    }

    public BuildDispatcher(BuildDispatcherOptions options, IBuildArtifactSelectorStrategy artifactSelectorStrategy)
        : this(
            options,
            new DefaultZigLocator(),
            new WorkspaceFactory(options),
            new ZigProcessRunner(options),
            artifactSelectorStrategy,
            new BuildArtifactReader())
    {
    }

    public BuildDispatcher(
        BuildDispatcherOptions options,
        IZigLocator zigLocator,
        IWorkspaceFactory workspaceFactory,
        IZigProcessRunner processRunner,
        IBuildArtifactSelectorStrategy artifactSelectorStrategy,
        IBuildArtifactReader artifactReader)
    {
        _options = options;
        _zigLocator = zigLocator;
        _workspaceFactory = workspaceFactory;
        _processRunner = processRunner;
        _artifactSelectorStrategy = artifactSelectorStrategy;
        _artifactReader = artifactReader;
        _concurrency = new SemaphoreSlim(Math.Max(1, options.MaxConcurrency));
    }

    public async ValueTask<Result<BuildResult>> DispatchAsync(
        BuildRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await TryWaitAsync(cancellationToken).ConfigureAwait(false))
        {
            CompleteOutput(request.OutputSubscriber);
            return Result<BuildResult>.Fail(BuildError.Cancelled(BuildResult.Empty));
        }

        try
        {
            return await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private async ValueTask<Result<BuildResult>> ExecuteAsync(
        BuildRequest request,
        CancellationToken cancellationToken)
    {
        var zigPathResult = _zigLocator.ResolveZigPath(request);
        if (!zigPathResult.IsSuccess)
        {
            CompleteOutput(request.OutputSubscriber);
            return Result<BuildResult>.Fail(zigPathResult.Error);
        }

        var workspaceResult = _workspaceFactory.Create(request);
        if (!workspaceResult.IsSuccess)
        {
            CompleteOutput(request.OutputSubscriber);
            return Result<BuildResult>.Fail(workspaceResult.Error);
        }

        var workspace = workspaceResult.Value;

        var runResult = await _processRunner
            .RunAsync(zigPathResult.Value, workspace, request, cancellationToken)
            .ConfigureAwait(false);

        if (!runResult.IsSuccess)
        {
            CleanupWorkspace(workspace, request, false);
            return runResult;
        }

        var selectionResult = _artifactSelectorStrategy.Resolve(request, runResult.Value);
        if (!selectionResult.IsSuccess)
        {
            CleanupWorkspace(workspace, request, false);
            return Result<BuildResult>.Fail(AttachOutput(selectionResult.Error, runResult.Value));
        }

        var selectedRequest = request with { ArtifactSelector = selectionResult.Value };
        var artifactResult = _artifactReader.Read(workspace, selectedRequest);
        if (!artifactResult.IsSuccess)
        {
            CleanupWorkspace(workspace, request, false);
            return Result<BuildResult>.Fail(AttachOutput(artifactResult.Error, runResult.Value));
        }

        var buildResult = runResult.Value with { Artifact = artifactResult.Value };
        CleanupWorkspace(workspace, request, true);

        return Result<BuildResult>.Ok(buildResult);
    }

    private async ValueTask<bool> TryWaitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void CleanupWorkspace(BuildWorkspace workspace, BuildRequest request, bool buildSucceeded)
    {
        var artifactCleanupMode = ResolveArtifactCleanupMode(request);
        _workspaceFactory.CleanupArtifacts(workspace, artifactCleanupMode);

        var shouldCleanup = ResolveWorkspaceCleanup(request, buildSucceeded);

        if (!shouldCleanup)
        {
            return;
        }

        _workspaceFactory.Cleanup(workspace);
    }

    private BuildArtifactCleanupMode ResolveArtifactCleanupMode(BuildRequest request)
        => request.ArtifactCleanupMode == BuildArtifactCleanupMode.Default
            ? _options.ArtifactCleanupMode
            : request.ArtifactCleanupMode;

    private bool ResolveWorkspaceCleanup(BuildRequest request, bool buildSucceeded)
    {
        return request.WorkspaceCleanupMode switch
        {
            WorkspaceCleanupMode.Always => true,
            WorkspaceCleanupMode.Never => false,
            _ => buildSucceeded
                ? _options.CleanupWorkspaceOnSuccess
                : _options.CleanupWorkspaceOnFailure
        };
    }

    private static BuildError AttachOutput(BuildError error, BuildResult output)
        => BuildError.WithOutput(error.Code, error.Message, output);

    private static void CompleteOutput(IBuildOutputSubscriber subscriber)
    {
        if (subscriber is not IBuildOutputSubscriberCompletion completion)
        {
            return;
        }

        try
        {
            completion.Complete();
        }
        catch
        {
        }
    }
}
