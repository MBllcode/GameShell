namespace GameShell.Core.Models;

public enum ConsoleSessionState
{
    Idle,
    EnteringProfile,
    LauncherActive,
    LaunchingGame,
    GameActive,
    ReturningToLauncher,
    ExitingToWorkMode,
    Faulted
}

public sealed record ConsoleSessionSnapshot
{
    public ConsoleSessionState State { get; init; } = ConsoleSessionState.Idle;
    public long SessionGeneration { get; init; }
    public string? ProfileName { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.Now;
}
