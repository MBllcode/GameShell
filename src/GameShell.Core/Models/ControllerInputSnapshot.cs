using System.Collections.Frozen;

namespace GameShell.Core.Models;

public sealed record ControllerInputSnapshot
{
    public ControllerInputSnapshot(
        string controllerId,
        IReadOnlySet<ControllerButton> pressedButtons,
        long sequenceNumber,
        DateTimeOffset timestamp)
    {
        ControllerId = controllerId;
        PressedButtons = pressedButtons.ToFrozenSet();
        SequenceNumber = sequenceNumber;
        Timestamp = timestamp;
    }

    public string ControllerId { get; }
    public IReadOnlySet<ControllerButton> PressedButtons { get; }
    public long SequenceNumber { get; }
    public DateTimeOffset Timestamp { get; }
}
