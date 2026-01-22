using System.Diagnostics;

namespace ZigBuildDispatcher;

public interface IZigLocator
{
    Result<string> ResolveZigPath(BuildRequest request);
}

public sealed class DefaultZigLocator : IZigLocator
{
    private readonly ZigHomeLocator _zigHomeLocator;
    private readonly ZvmZigLocator _zvmLocator;
    private readonly ZvmHomeZigLocator _zvmHomeLocator;
    private readonly PathZigLocator _pathLocator;

    public DefaultZigLocator()
        : this(new ZigHomeLocator(), new ZvmZigLocator(), new ZvmHomeZigLocator(), new PathZigLocator())
    {
    }

    public DefaultZigLocator(
        ZigHomeLocator zigHomeLocator,
        ZvmZigLocator zvmLocator,
        ZvmHomeZigLocator zvmHomeLocator,
        PathZigLocator pathLocator)
    {
        _zigHomeLocator = zigHomeLocator;
        _zvmLocator = zvmLocator;
        _zvmHomeLocator = zvmHomeLocator;
        _pathLocator = pathLocator;
    }

    public Result<string> ResolveZigPath(BuildRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ZigHome))
        {
            return _zigHomeLocator.ResolveZigPath(request);
        }

        var zvmResult = _zvmLocator.ResolveZigPath(request);
        if (zvmResult.IsSuccess)
        {
            return zvmResult;
        }

        if (zvmResult.Error.Code != ErrorCodes.ZvmNotFound
            && zvmResult.Error.Code != ErrorCodes.ZigNotFound)
        {
            return zvmResult;
        }

        var zvmHomeResult = _zvmHomeLocator.ResolveZigPath(request);
        if (zvmHomeResult.IsSuccess)
        {
            return zvmHomeResult;
        }

        if (zvmHomeResult.Error.Code != ErrorCodes.ZvmNotFound
            && zvmHomeResult.Error.Code != ErrorCodes.ZigNotFound)
        {
            return zvmHomeResult;
        }

        var pathResult = _pathLocator.ResolveZigPath(request);
        if (pathResult.IsSuccess)
        {
            return pathResult;
        }

        if (zvmResult.Error.Code == ErrorCodes.ZigNotFound
            || zvmHomeResult.Error.Code == ErrorCodes.ZigNotFound)
        {
            return Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZigNotFound,
                "zvm did not return a zig path, zvm home did not contain zig, and zig was not found on PATH."));
        }

        return pathResult;
    }
}

public sealed class ZigHomeLocator : IZigLocator
{
    public Result<string> ResolveZigPath(BuildRequest request)
    {
        var zigHome = request.ZigHome;
        if (string.IsNullOrWhiteSpace(zigHome))
        {
            return Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZigNotFound,
                "Zig home was not provided."));
        }

        return ResolveFromZigHome(zigHome);
    }

    private static Result<string> ResolveFromZigHome(string zigHome)
    {
        if (File.Exists(zigHome))
        {
            return Result<string>.Ok(zigHome);
        }

        if (!Directory.Exists(zigHome))
        {
            return Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZigNotFound,
                $"Zig home '{zigHome}' does not exist."));
        }

        var exeName = ToolPathResolver.GetZigExecutableName();
        var candidate = Path.Combine(zigHome, exeName);

        return File.Exists(candidate)
            ? Result<string>.Ok(candidate)
            : Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZigNotFound,
                $"Zig was not found at '{candidate}'."));
    }
}

public sealed class PathZigLocator : IZigLocator
{
    public Result<string> ResolveZigPath(BuildRequest request)
        => ToolPathResolver.ResolveFromPath(
            ToolPathResolver.GetZigExecutableName(),
            ErrorCodes.ZigNotFound,
            "Zig was not found on PATH.");
}

public sealed class ZvmZigLocator : IZigLocator
{
    private readonly IZvmCommandRunner _commandRunner;

    public ZvmZigLocator()
        : this(new ZvmCommandRunner())
    {
    }

    public ZvmZigLocator(IZvmCommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
    }

    public Result<string> ResolveZigPath(BuildRequest request)
    {
        var zvmPathResult = ToolPathResolver.ResolveFromPath(
            ToolPathResolver.GetZvmExecutableName(),
            ErrorCodes.ZvmNotFound,
            "zvm was not found on PATH.");

        if (!zvmPathResult.IsSuccess)
        {
            return Result<string>.Fail(zvmPathResult.Error);
        }

        var commands = new[]
        {
            new[] { "which", "zig" },
            new[] { "which" }
        };

        foreach (var args in commands)
        {
            var result = _commandRunner.ResolveZigPath(zvmPathResult.Value, args);
            if (result.IsSuccess)
            {
                return result;
            }

            if (result.Error.Code != ErrorCodes.ZigNotFound)
            {
                return result;
            }
        }

        return Result<string>.Fail(BuildError.WithoutOutput(
            ErrorCodes.ZigNotFound,
            "zvm did not return a zig path."));
    }
}

public sealed class ZvmHomeZigLocator : IZigLocator
{
    public Result<string> ResolveZigPath(BuildRequest request)
    {
        var zvmHomeResult = TryResolveZvmHome();
        if (!zvmHomeResult.IsSuccess)
        {
            return Result<string>.Fail(zvmHomeResult.Error);
        }

        var zvmHome = zvmHomeResult.Value;
        var zigExe = ToolPathResolver.GetZigExecutableName();
        var binCandidate = Path.Combine(zvmHome, "bin", zigExe);
        if (File.Exists(binCandidate))
        {
            return Result<string>.Ok(binCandidate);
        }

        var versionResult = FindLatestInstalledZig(zvmHome, zigExe);
        return versionResult.IsSuccess
            ? Result<string>.Ok(versionResult.Value)
            : Result<string>.Fail(versionResult.Error);
    }

    private static Result<string> TryResolveZvmHome()
    {
        var envHome = Environment.GetEnvironmentVariable("ZVM_HOME");
        if (!string.IsNullOrWhiteSpace(envHome) && Directory.Exists(envHome))
        {
            return Result<string>.Ok(envHome);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZvmNotFound,
                "User profile could not be resolved for zvm home."));
        }

        var defaultHome = Path.Combine(userProfile, ".zvm");
        return Directory.Exists(defaultHome)
            ? Result<string>.Ok(defaultHome)
            : Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZvmNotFound,
                "zvm home directory was not found."));
    }

    private static Result<string> FindLatestInstalledZig(string zvmHome, string zigExe)
    {
        try
        {
            var bestPath = string.Empty;
            Version? bestVersion = null;

            foreach (var directory in Directory.EnumerateDirectories(zvmHome))
            {
                var name = Path.GetFileName(directory);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("self", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var candidate = Path.Combine(directory, zigExe);
                if (!File.Exists(candidate))
                {
                    continue;
                }

                if (!Version.TryParse(name, out var parsed))
                {
                    if (string.IsNullOrWhiteSpace(bestPath))
                    {
                        bestPath = candidate;
                    }

                    continue;
                }

                if (bestVersion is null || parsed > bestVersion)
                {
                    bestVersion = parsed;
                    bestPath = candidate;
                }
            }

            return string.IsNullOrWhiteSpace(bestPath)
                ? Result<string>.Fail(BuildError.WithoutOutput(
                    ErrorCodes.ZigNotFound,
                    "No zig binaries were found under zvm home."))
                : Result<string>.Ok(bestPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZigNotFound,
                $"Failed to search zvm home for zig: {ex.Message}"));
        }
    }
}

public interface IZvmCommandRunner
{
    Result<string> ResolveZigPath(string zvmPath, IReadOnlyList<string> arguments);
}

public sealed class ZvmCommandRunner : IZvmCommandRunner
{
    public Result<string> ResolveZigPath(string zvmPath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = zvmPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return Result<string>.Fail(BuildError.WithoutOutput(
                    ErrorCodes.ZigNotFound,
                    "Failed to start zvm."));
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stderr)
                    ? "zvm command failed."
                    : $"zvm command failed: {stderr.Trim()}";

                return Result<string>.Fail(BuildError.WithoutOutput(ErrorCodes.ZigNotFound, message));
            }

            return ParseZvmOutput(stdout);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZigNotFound,
                $"zvm execution failed: {ex.Message}"));
        }
    }

    private static Result<string> ParseZvmOutput(string output)
    {
        var line = output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(line))
        {
            return Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZigNotFound,
                "zvm did not report a zig path."));
        }

        var candidate = ExtractPathCandidate(line);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZigNotFound,
                $"zvm returned an invalid path '{line}'."));
        }

        candidate = NormalizePath(candidate);

        return File.Exists(candidate)
            ? Result<string>.Ok(candidate)
            : Result<string>.Fail(BuildError.WithoutOutput(
                ErrorCodes.ZigNotFound,
                $"zvm returned '{candidate}', but it does not exist."));
    }

    private static string ExtractPathCandidate(string line)
    {
        var trimmed = line.Trim().Trim('\"');
        if (File.Exists(trimmed))
        {
            return trimmed;
        }

        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        var last = tokens[^1].Trim().Trim('\"');
        return last;
    }

    private static string NormalizePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (expanded.StartsWith("~", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var rest = expanded.Length > 1
                ? expanded[1] == Path.DirectorySeparatorChar || expanded[1] == Path.AltDirectorySeparatorChar
                    ? expanded[2..]
                    : expanded[1..]
                : string.Empty;

            return Path.GetFullPath(Path.Combine(home, rest));
        }

        return Path.GetFullPath(expanded);
    }
}

internal static class ToolPathResolver
{
    public static Result<string> ResolveFromPath(string toolName, string errorCode, string notFoundMessage)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return Result<string>.Fail(BuildError.WithoutOutput(
                errorCode,
                $"{notFoundMessage} PATH was empty."));
        }

        foreach (var segment in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            var candidate = Path.Combine(segment, toolName);
            if (File.Exists(candidate))
            {
                return Result<string>.Ok(candidate);
            }
        }

        return Result<string>.Fail(BuildError.WithoutOutput(errorCode, notFoundMessage));
    }

    public static string GetZigExecutableName()
        => OperatingSystem.IsWindows() ? "zig.exe" : "zig";

    public static string GetZvmExecutableName()
        => OperatingSystem.IsWindows() ? "zvm.exe" : "zvm";
}
