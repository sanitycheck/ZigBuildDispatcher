using System.Globalization;

var prefix = string.Empty;
var cacheDir = string.Empty;
var globalCacheDir = string.Empty;
var artifactName = "app.bin";
var artifactLocation = "bin";
var stdoutLines = 0;
var stderrLines = 0;
var delayMs = 0;
var exitCode = 0;

for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];
    if (arg == "--prefix" && TryGetNext(args, ref i, out var prefixValue))
    {
        prefix = prefixValue;
        continue;
    }

    if (arg == "--cache-dir" && TryGetNext(args, ref i, out var cacheValue))
    {
        cacheDir = cacheValue;
        continue;
    }

    if (arg == "--global-cache-dir" && TryGetNext(args, ref i, out var globalValue))
    {
        globalCacheDir = globalValue;
        continue;
    }

    if (arg.StartsWith("-Dartifact=", StringComparison.Ordinal))
    {
        artifactName = arg["-Dartifact=".Length..];
        continue;
    }

    if (arg.StartsWith("-DartifactLocation=", StringComparison.Ordinal))
    {
        artifactLocation = arg["-DartifactLocation=".Length..];
        continue;
    }

    if (arg.StartsWith("-DstdoutLines=", StringComparison.Ordinal)
        && TryParseInt(arg["-DstdoutLines=".Length..], out var parsedStdout))
    {
        stdoutLines = parsedStdout;
        continue;
    }

    if (arg.StartsWith("-DstderrLines=", StringComparison.Ordinal)
        && TryParseInt(arg["-DstderrLines=".Length..], out var parsedStderr))
    {
        stderrLines = parsedStderr;
        continue;
    }

    if (arg.StartsWith("-DdelayMs=", StringComparison.Ordinal)
        && TryParseInt(arg["-DdelayMs=".Length..], out var parsedDelay))
    {
        delayMs = parsedDelay;
        continue;
    }

    if (arg.StartsWith("-Dexit=", StringComparison.Ordinal)
        && TryParseInt(arg["-Dexit=".Length..], out var parsedExit))
    {
        exitCode = parsedExit;
        continue;
    }
}

WriteLines(Console.Out, "stdout", stdoutLines);
WriteLines(Console.Error, "stderr", stderrLines);

if (delayMs > 0)
{
    Thread.Sleep(delayMs);
}

EnsureMarker(cacheDir, "cache.txt");
EnsureMarker(globalCacheDir, "global-cache.txt");

if (!string.IsNullOrWhiteSpace(prefix))
{
    var outputDir = artifactLocation.Equals("out", StringComparison.OrdinalIgnoreCase)
        ? prefix
        : Path.Combine(prefix, "bin");

    EnsureDirectory(outputDir);
    var artifactPath = Path.Combine(outputDir, artifactName);
    File.WriteAllText(artifactPath, $"artifact:{artifactName}");
}

return exitCode;

static bool TryGetNext(string[] arguments, ref int index, out string value)
{
    var nextIndex = index + 1;
    if (nextIndex < arguments.Length)
    {
        index = nextIndex;
        value = arguments[index];
        return true;
    }

    value = string.Empty;
    return false;
}

static bool TryParseInt(string value, out int parsed)
{
    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
}

static void WriteLines(TextWriter writer, string prefix, int count)
{
    for (var i = 0; i < count; i++)
    {
        writer.WriteLine($"{prefix}-{i}");
    }
}

static void EnsureMarker(string directory, string fileName)
{
    if (string.IsNullOrWhiteSpace(directory))
    {
        return;
    }

    EnsureDirectory(directory);
    File.WriteAllText(Path.Combine(directory, fileName), "marker");
}

static void EnsureDirectory(string directory)
{
    if (string.IsNullOrWhiteSpace(directory))
    {
        return;
    }

    Directory.CreateDirectory(directory);
}
