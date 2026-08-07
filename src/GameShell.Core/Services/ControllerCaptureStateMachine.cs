using GameShell.Core.Models;
using System.Collections.Frozen;

namespace GameShell.Core.Services;

public sealed class ControllerCaptureStateMachine
{
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CombinationWindow = TimeSpan.FromMilliseconds(300);

    private long _captureSequence;
    private DateTimeOffset _startedAt;
    private DateTimeOffset _firstPressAt;
    private string? _controllerId;
    private readonly HashSet<ControllerButton> _capturedButtons = [];

    public ControllerCapturePhase Phase { get; private set; } = ControllerCapturePhase.Idle;
    public IReadOnlySet<ControllerButton> CapturedButtons => _capturedButtons.ToFrozenSet();
    public string? ControllerId => _controllerId;

    public void Start(ControllerInputSnapshot latestSnapshot, DateTimeOffset? startedAt = null)
    {
        _captureSequence = latestSnapshot.SequenceNumber;
        _startedAt = startedAt ?? latestSnapshot.Timestamp;
        _firstPressAt = default;
        _controllerId = null;
        _capturedButtons.Clear();
        Phase = latestSnapshot.PressedButtons.Count == 0
            ? ControllerCapturePhase.WaitingForFirstPress
            : ControllerCapturePhase.WaitingForRelease;
    }

    public void Process(ControllerInputSnapshot snapshot)
    {
        if (Phase is ControllerCapturePhase.Idle or ControllerCapturePhase.Completed or ControllerCapturePhase.Cancelled or ControllerCapturePhase.TimedOut
            || snapshot.SequenceNumber <= _captureSequence)
        {
            return;
        }

        if (snapshot.Timestamp - _startedAt >= CaptureTimeout)
        {
            Phase = ControllerCapturePhase.TimedOut;
            return;
        }

        switch (Phase)
        {
            case ControllerCapturePhase.WaitingForRelease:
                if (snapshot.PressedButtons.Count == 0)
                {
                    Phase = ControllerCapturePhase.WaitingForFirstPress;
                }
                break;

            case ControllerCapturePhase.WaitingForFirstPress:
                if (snapshot.PressedButtons.Count > 0)
                {
                    _controllerId = snapshot.ControllerId;
                    _capturedButtons.UnionWith(snapshot.PressedButtons);
                    _firstPressAt = snapshot.Timestamp;
                    Phase = ControllerCapturePhase.Recording;
                }
                break;

            case ControllerCapturePhase.Recording:
                if (!string.Equals(_controllerId, snapshot.ControllerId, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                _capturedButtons.UnionWith(snapshot.PressedButtons);
                if (snapshot.PressedButtons.Count == 0)
                {
                    Phase = ControllerCapturePhase.Completed;
                }
                else if (snapshot.Timestamp - _firstPressAt >= CombinationWindow)
                {
                    Phase = ControllerCapturePhase.Completed;
                }
                break;

            case ControllerCapturePhase.WaitingForCombinationRelease:
                if (!snapshot.PressedButtons.Overlaps(_capturedButtons))
                {
                    Phase = ControllerCapturePhase.Completed;
                }
                break;
        }

        _captureSequence = snapshot.SequenceNumber;
    }

    public void Cancel()
    {
        _capturedButtons.Clear();
        _controllerId = null;
        Phase = ControllerCapturePhase.Cancelled;
    }

    public void Reset()
    {
        _capturedButtons.Clear();
        _controllerId = null;
        Phase = ControllerCapturePhase.Idle;
    }
}
