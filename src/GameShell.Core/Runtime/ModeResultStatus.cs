namespace GameShell.Core.Runtime;

public enum ModeResultStatus
{
    Success,
    NoChanges,
    Failed,
    Cancelled,
    TimedOut,
    AlreadyRunning,
    DependencyMissing,
    InvalidConfiguration
}
