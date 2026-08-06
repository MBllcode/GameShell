namespace GameShell.Core.Models;

public enum ModeResultStatus
{
    Success,
    Failed,
    Cancelled,
    TimedOut,
    AlreadyRunning,
    DependencyMissing,
    InvalidConfiguration
}
