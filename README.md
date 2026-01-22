# ZigBuildDispatcher

High-performance build dispatcher for Zig projects with concurrent builds, isolated workspaces, and streamed output.

## Highlights
- Concurrent build execution with a bounded semaphore.
- Per-build workspace isolation (zig-cache/zig-out/zig-tmp).
- Optional shared global cache for faster repeated builds.
- Output streaming without exceptions (railway-style `Result<T>`).
- Artifact selection by name, extension, pattern, or explicit path.
- Zig discovery via explicit `ZigHome`, zvm, zvm home, or PATH.

## Requirements
- .NET 10.0 (projects target `net10.0`).
- Zig installed (or managed via zvm).

## Quick Start
```csharp
using ZigBuildDispatcher;

var options = new BuildDispatcherOptions
{
    MaxConcurrency = Math.Max(1, Environment.ProcessorCount),
    WorkspaceRoot = Path.Combine(Path.GetTempPath(), "zig-build-dispatcher"),
    SharedGlobalCacheDir = Path.Combine(Path.GetTempPath(), "zig-build-dispatcher", "zig-global-cache"),
    CleanupWorkspaceOnSuccess = true,
    CleanupWorkspaceOnFailure = false
};

var dispatcher = new BuildDispatcher(options);

var request = BuildRequest.Create(
        buildZigPath: @"C:\path\to\build.zig",
        "-Doptimize=Debug",
        "--summary", "all")
    with
    {
        // Optional: set to a file path or Zig install directory.
        ZigHome = string.Empty,
        // Optional: select output if auto-detect is ambiguous.
        ArtifactSelector = BuildArtifactSelector.Auto,
        // Optional: reuse cache/workspace per user/session.
        CacheKey = "user-123",
        WorkspaceKey = "tab-456"
    };

var result = await dispatcher.DispatchAsync(request);
if (!result.IsSuccess)
{
    Console.WriteLine($"Build failed: {result.Error.Code} - {result.Error.Message}");
    return;
}

var build = result.Value;
Console.WriteLine($"Exit code: {build.ExitCode}");
Console.WriteLine($"Artifact path: {build.Artifact.Path}");
Console.WriteLine($"Artifact bytes: {build.Artifact.Bytes.Length}");
```

## Output Streaming
Use `BuildOutputChannel` for realtime output:
```csharp
var output = new BuildOutputChannel(new BuildOutputChannelOptions
{
    Capacity = 2048,
    SingleReader = true
});

var request = BuildRequest.Create(buildZigPath, "--summary", "all") with
{
    OutputSubscriber = output
};

var readTask = Task.Run(async () =>
{
    await foreach (var line in output.ReadAllAsync())
    {
        Console.WriteLine(line.Text);
    }
});

var result = await dispatcher.DispatchAsync(request);
output.Complete();
await readTask;
```

Note: Zig only emits full progress output on a real TTY. If you want output when redirected, include `--summary all` as shown above.

## Artifact Selection
Auto-detect tries to find a single artifact in `zig-out/bin` (then `zig-out`) and ignores common debug sidecars.

Explicit selectors:
```csharp
BuildArtifactSelector.RelativePath("bin/mytool.exe")
BuildArtifactSelector.FileName("mytool.exe")
BuildArtifactSelector.Extension(".dll")
BuildArtifactSelector.Pattern("plugin-*.xll")
```

You can also select by build arguments if you configure a selector strategy:
```csharp
var rules = new[]
{
    BuildArgumentSelectorRule.FileNamePrefix("-Dartifact="),
    BuildArgumentSelectorRule.ExtensionPrefix("-DartifactExt="),
    BuildArgumentSelectorRule.PatternPrefix("-DartifactPattern=")
};

var dispatcher = new BuildDispatcher(options, new BuildArgumentSelectorStrategy(rules));
```

Example arguments:
```
-Dartifact=foo.exe
-DartifactExt=.dll
-DartifactPattern=plugin-*.xll
```

## Workspace and Caching
Every build uses a workspace with these folders:
- `zig-cache` (per build)
- `zig-out` (per build)
- `zig-tmp` (per build)
- `zig-global-cache` (per build unless `SharedGlobalCacheDir` is set)

Keys:
- `CacheKey` namespaces the workspace root and shared global cache per user.
- `WorkspaceKey` reuses the workspace root (per tab/session) to avoid re-copying.

Cleanup:
```csharp
// Default is All (cache/out/tmp).
var request = BuildRequest.Create(buildZigPath) with
{
    ArtifactCleanupMode = BuildArtifactCleanupMode.OutputOnly, // keep caches
    WorkspaceCleanupMode = WorkspaceCleanupMode.Never          // keep workspace root
};
```

## Zig Discovery
The dispatcher resolves the Zig binary in this order:
1. `BuildRequest.ZigHome` (file path or install directory).
2. `zvm` (via `zvm which zig` or `zvm which`).
3. zvm home (`ZVM_HOME` or `%USERPROFILE%\.zvm`, selects latest version).
4. `PATH`.

## Errors and Results (Railway Style)
All APIs return `Result<T>` instead of throwing:
- `Result.IsSuccess == true` -> use `Result.Value`.
- `Result.IsSuccess == false` -> inspect `Result.Error` and `Result.Error.Output`.

## Sample App
Run the Blazor sample:
```
dotnet run --project ZigBuildDispatcher.Sample
```

The Build page uses XtermBlazor for live output streaming and exposes cache/workspace knobs.

## Tests
```
dotnet test ZigBuildDispatcher.Tests
```

Performance tests are opt-in:
```
set RUN_PERF_TESTS=1
dotnet test ZigBuildDispatcher.Tests
```
