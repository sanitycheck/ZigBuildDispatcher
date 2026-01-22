namespace ZigBuildDispatcher;

public static class ErrorCodes
{
    public const string None = "none";
    public const string ZigNotFound = "zig_not_found";
    public const string InvalidBuildFile = "invalid_build_file";
    public const string WorkspaceCreateFailed = "workspace_create_failed";
    public const string CleanupFailed = "cleanup_failed";
    public const string ProcessStartFailed = "process_start_failed";
    public const string ProcessFailed = "process_failed";
    public const string BuildFailed = "build_failed";
    public const string Cancelled = "cancelled";
    public const string CommandBuildFailed = "command_build_failed";
    public const string ZvmNotFound = "zvm_not_found";
    public const string ArtifactNotFound = "artifact_not_found";
    public const string ArtifactAmbiguous = "artifact_ambiguous";
    public const string ArtifactReadFailed = "artifact_read_failed";
    public const string ArtifactSelectorFailed = "artifact_selector_failed";
}
