namespace GameShell.Core.Models;

public enum DisplayRecoveryResult
{
    NotAttempted,
    Succeeded,
    Failed,
    AccessDenied,
    TargetNotResolved
}

public sealed record DisplayRecoveryAttempt
{
    public string ProfileName { get; init; } = string.Empty;
    public ProfileDisplayTarget SavedTarget { get; init; } = new();
    public DisplayDeviceInfo ResolvedTarget { get; init; } = new();
    public string Mechanism { get; init; } = string.Empty;
    public string DevNodeInstanceId { get; init; } = string.Empty;
    public IReadOnlyList<DisplayModeInfo> BeforeModes { get; init; } = [];
    public int? LocateDevNodeResultCode { get; init; }
    public int? ReenumerateDevNodeResultCode { get; init; }
    public long WaitMs { get; init; }
    public DisplayRecoveryResult Result { get; init; }
}
