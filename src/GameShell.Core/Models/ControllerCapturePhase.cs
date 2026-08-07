namespace GameShell.Core.Models;

public enum ControllerCapturePhase
{
    Idle,
    WaitingForRelease,
    WaitingForFirstPress,
    Recording,
    WaitingForCombinationRelease,
    Completed,
    Cancelled,
    TimedOut
}
