namespace GameShell.Core.Models;

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
