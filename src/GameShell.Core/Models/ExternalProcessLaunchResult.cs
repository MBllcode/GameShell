namespace GameShell.Core.Models;

public sealed record ExternalProcessLaunchResult
{
    public bool Success { get; init; }
    public int? ProcessId { get; init; }
    public int? Win32ErrorCode { get; init; }
    public string? FailureReason { get; init; }

    public static ExternalProcessLaunchResult Started(int? processId = null) => new()
    {
        Success = true,
        ProcessId = processId
    };

    public static ExternalProcessLaunchResult Failed(string reason, int? win32ErrorCode = null) => new()
    {
        FailureReason = reason,
        Win32ErrorCode = win32ErrorCode
    };
}
