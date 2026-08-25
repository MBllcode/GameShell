namespace GameShell.Core.Models;

public enum TransientOperationKind
{
    None,
    Startup,
    EnterProfile,
    ExitToWorkMode,
    DisplayDiscovery,
    DisplayTransition,
    LauncherStartup
}
public enum TransientOperationPhase
{
    None,
    PreparingDisplays,
    ApplyingDisplayTopology,
    SynchronizingDisplays,
    StartingLauncher,
    ActivatingLauncher,
    RestoringDisplays,
    RestoringDesktop,
    Completed,
    Failed
}

public sealed record TransientOperationState
{
    public static TransientOperationState None { get; } = new();

    public TransientOperationKind Kind { get; init; } = TransientOperationKind.None;
    public TransientOperationPhase Phase { get; init; } = TransientOperationPhase.None;
    public bool IsActive => Kind != TransientOperationKind.None;
    public string? MessageKey { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public string? TransitionId { get; init; }
}
