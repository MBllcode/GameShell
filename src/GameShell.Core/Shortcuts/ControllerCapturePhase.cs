namespace GameShell.Core.Shortcuts;

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
