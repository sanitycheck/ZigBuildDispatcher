using System.Diagnostics;
using System.Text;

namespace ZigBuildDispatcher;

public interface IZigProcessRunner
{
    ValueTask<Result<BuildResult>> RunAsync(
        string zigPath,
        BuildWorkspace workspace,
        BuildRequest request,
        CancellationToken cancellationToken);
}

public sealed class ZigProcessRunner : IZigProcessRunner
{
    private readonly BuildDispatcherOptions _options;
    private readonly ZigCommandBuilder _commandBuilder = new();

    public ZigProcessRunner(BuildDispatcherOptions options)
    {
        _options = options;
    }

    public async ValueTask<Result<BuildResult>> RunAsync(
        string zigPath,
        BuildWorkspace workspace,
        BuildRequest request,
        CancellationToken cancellationToken)
    {
        var commandResult = _commandBuilder.Build(zigPath, workspace, request);
        if (!commandResult.IsSuccess)
        {
            CompleteOutput(request.OutputSubscriber);
            return Result<BuildResult>.Fail(commandResult.Error);
        }

        var command = commandResult.Value;
        var stdout = new BoundedStringBuilder(_options.MaxOutputChars);
        var stderr = new BoundedStringBuilder(_options.MaxOutputChars);
        var outputSubscriber = request.OutputSubscriber;

        var startInfo = new ProcessStartInfo
        {
            FileName = command.ZigPath,
            WorkingDirectory = command.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in command.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!process.Start())
            {
                stopwatch.Stop();
                return Result<BuildResult>.Fail(BuildError.WithoutOutput(
                    ErrorCodes.ProcessStartFailed,
                    "Failed to start zig process."));
            }

            var stdoutTask = ReadStreamAsync(
                process.StandardOutput,
                BuildOutputKind.StandardOutput,
                stdout,
                outputSubscriber,
                cancellationToken);

            var stderrTask = ReadStreamAsync(
                process.StandardError,
                BuildOutputKind.StandardError,
                stderr,
                outputSubscriber,
                cancellationToken);

            using var registration = cancellationToken.Register(() => TryKill(process));

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                await AwaitReadersAsync(stdoutTask, stderrTask).ConfigureAwait(false);
                var cancelledResult = new BuildResult(
                    -1,
                    stdout.Build(),
                    stderr.Build(),
                    stdout.IsTruncated,
                    stderr.IsTruncated,
                    stopwatch.Elapsed,
                    workspace,
                    BuildArtifact.Empty);

                return Result<BuildResult>.Fail(BuildError.Cancelled(cancelledResult));
            }

            process.WaitForExit();
            stopwatch.Stop();
            await AwaitReadersAsync(stdoutTask, stderrTask).ConfigureAwait(false);

            var result = new BuildResult(
                process.ExitCode,
                stdout.Build(),
                stderr.Build(),
                stdout.IsTruncated,
                stderr.IsTruncated,
                stopwatch.Elapsed,
                workspace,
                BuildArtifact.Empty);

            return process.ExitCode == 0
                ? Result<BuildResult>.Ok(result)
                : Result<BuildResult>.Fail(BuildError.BuildFailed(result));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Result<BuildResult>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ProcessFailed,
                $"Process execution failed: {ex.Message}"));
        }
        finally
        {
            CompleteOutput(outputSubscriber);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(true);
        }
        catch
        {
            // Best effort to stop the process.
        }
    }

    private static void NotifyOutput(IBuildOutputSubscriber subscriber, BuildOutputLine line)
    {
        try
        {
            subscriber.OnOutput(line);
        }
        catch
        {
        }
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        BuildOutputKind kind,
        BoundedStringBuilder buffer,
        IBuildOutputSubscriber subscriber,
        CancellationToken cancellationToken)
    {
        var lineBuffer = new StringBuilder();
        var chunkBuffer = new char[256];
        var sawCarriageReturn = false;

        try
        {
            while (true)
            {
                var read = await reader.ReadAsync(chunkBuffer.AsMemory(0, chunkBuffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                for (var i = 0; i < read; i++)
                {
                    var ch = chunkBuffer[i];
                    if (ch == '\r')
                    {
                        FlushLine(kind, lineBuffer, buffer, subscriber);
                        sawCarriageReturn = true;
                        continue;
                    }

                    if (ch == '\n')
                    {
                        if (sawCarriageReturn)
                        {
                            sawCarriageReturn = false;
                            continue;
                        }

                        FlushLine(kind, lineBuffer, buffer, subscriber);
                        continue;
                    }

                    sawCarriageReturn = false;
                    lineBuffer.Append(ch);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            FlushLine(kind, lineBuffer, buffer, subscriber);
        }
    }

    private static void FlushLine(
        BuildOutputKind kind,
        StringBuilder lineBuffer,
        BoundedStringBuilder outputBuffer,
        IBuildOutputSubscriber subscriber)
    {
        if (lineBuffer.Length == 0)
        {
            return;
        }

        var text = lineBuffer.ToString();
        lineBuffer.Clear();

        NotifyOutput(subscriber, new BuildOutputLine(kind, text));
        outputBuffer.AppendLine(text);
    }

    private static async Task AwaitReadersAsync(Task stdoutTask, Task stderrTask)
    {
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch
        {
        }
    }

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

    private sealed class ZigCommandBuilder
    {
        public Result<ZigCommand> Build(
            string zigPath,
            BuildWorkspace workspace,
            BuildRequest request)
        {
            if (!File.Exists(request.BuildZigPath))
            {
                return Result<ZigCommand>.Fail(BuildError.WithoutOutput(
                    ErrorCodes.InvalidBuildFile,
                    $"build.zig path '{request.BuildZigPath}' does not exist."));
            }

            var workingDirectory = Path.GetDirectoryName(request.BuildZigPath);
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                return Result<ZigCommand>.Fail(BuildError.WithoutOutput(
                    ErrorCodes.InvalidBuildFile,
                    $"build.zig path '{request.BuildZigPath}' is invalid."));
            }

            var args = new List<string>(6 + request.BuildArguments.Count)
            {
                "build",
                "--build-file",
                request.BuildZigPath,
                "--cache-dir",
                workspace.CacheDir,
                "--global-cache-dir",
                workspace.GlobalCacheDir,
                "--prefix",
                workspace.OutDir
            };

            foreach (var arg in request.BuildArguments)
            {
                args.Add(arg);
            }

            return Result<ZigCommand>.Ok(new ZigCommand(zigPath, workingDirectory, args));
        }
    }

    private sealed record ZigCommand(
        string ZigPath,
        string WorkingDirectory,
        IReadOnlyList<string> Arguments);

    private sealed class BoundedStringBuilder
    {
        private readonly int _maxChars;
        private readonly StringBuilder _builder;

        public BoundedStringBuilder(int maxChars)
        {
            _maxChars = Math.Max(0, maxChars);
            _builder = new StringBuilder(Math.Min(_maxChars, 4096));
        }

        public bool IsTruncated { get; private set; }

        public void AppendLine(string value)
        {
            if (_maxChars == 0)
            {
                IsTruncated = true;
                return;
            }

            var newLine = Environment.NewLine;
            var required = value.Length + newLine.Length;

            if (_builder.Length + required <= _maxChars)
            {
                _builder.Append(value);
                _builder.Append(newLine);
                return;
            }

            IsTruncated = true;
        }

        public string Build()
        {
            return _builder.ToString();
        }
    }
}
