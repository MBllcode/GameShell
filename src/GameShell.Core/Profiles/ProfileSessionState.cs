namespace GameShell.Core.Profiles;

public enum ProfileSessionState
{
    Idle,
    EnteringProfile,
    LauncherActive,
    LaunchingGame,
    GameActive,
    ReturningToLauncher,
    ExitingToWorkMode,
    Faulted,
    // Append to preserve the numeric values of existing integrations.
    ProfileActive
}

public sealed record ProfileSessionSnapshot
{
    public ProfileSessionState State { get; init; } = ProfileSessionState.Idle;
    public long SessionGeneration { get; init; }
    public string? ProfileName { get; init; }
    public string? ActivationFingerprint { get; init; }
    public string? LastError { get; init; }
    public int? ActiveGamePid { get; init; }
    public string? ActiveGameProcess { get; init; }
    public int? LauncherPid { get; init; }
    public string? LauncherProcess { get; init; }
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.Now;
}
