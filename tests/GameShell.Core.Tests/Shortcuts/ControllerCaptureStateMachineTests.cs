using GameShell.Core.Shortcuts;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ControllerCaptureStateMachineTests
{
    private static readonly DateTimeOffset StartTime = DateTimeOffset.Parse("2026-08-07T04:15:00Z");

    [Fact]
    public void DetectsPressSeveralSecondsAfterCaptureStarts()
    {
        var machine = StartWithEmptySnapshot();

        machine.Process(Snapshot(2, 5, ControllerButton.South));

        Assert.Equal(ControllerCapturePhase.Recording, machine.Phase);
        Assert.Equal([ControllerButton.South], machine.CapturedButtons);
    }

    [Fact]
    public void CollectsButtonsPressedWithinCombinationWindow()
    {
        var machine = StartWithEmptySnapshot();

        machine.Process(Snapshot(2, 5, ControllerButton.South));
        machine.Process(Snapshot(3, 100, ControllerButton.South, ControllerButton.West));
        machine.Process(Snapshot(4, 320, ControllerButton.South, ControllerButton.West));

        Assert.Equal(ControllerCapturePhase.Completed, machine.Phase);
        Assert.Equal([ControllerButton.South, ControllerButton.West], machine.CapturedButtons.OrderBy(button => button));
    }

    [Fact]
    public void CompletesRecognizedCombinationWhileButtonsAreStillHeld()
    {
        var machine = StartWithEmptySnapshot();

        machine.Process(Snapshot(2, 5, ControllerButton.South));
        machine.Process(Snapshot(3, 100, ControllerButton.South, ControllerButton.West));
        machine.Process(Snapshot(4, 320, ControllerButton.South, ControllerButton.West));

        Assert.Equal(ControllerCapturePhase.Completed, machine.Phase);
        Assert.Equal([ControllerButton.South, ControllerButton.West], machine.CapturedButtons.OrderBy(button => button));
    }

    [Fact]
    public void RecordsSimultaneousButtons()
    {
        var machine = StartWithEmptySnapshot();

        machine.Process(Snapshot(2, 5, ControllerButton.South, ControllerButton.West));
        machine.Process(Snapshot(3, 400));

        Assert.Equal(ControllerCapturePhase.Completed, machine.Phase);
        Assert.Equal([ControllerButton.South, ControllerButton.West], machine.CapturedButtons.OrderBy(button => button));
    }

    [Fact]
    public void HeldButtonsBeforeStartAreReleasedBeforeRecording()
    {
        var machine = new ControllerCaptureStateMachine();
        machine.Start(Snapshot(10, 0, ControllerButton.South));

        machine.Process(Snapshot(11, 100, ControllerButton.South));
        Assert.Equal(ControllerCapturePhase.WaitingForRelease, machine.Phase);
        machine.Process(Snapshot(12, 200));
        Assert.Equal(ControllerCapturePhase.WaitingForFirstPress, machine.Phase);
        machine.Process(Snapshot(13, 300, ControllerButton.West));

        Assert.Equal([ControllerButton.West], machine.CapturedButtons);
    }

    [Fact]
    public void IgnoresSecondControllerDuringRecording()
    {
        var machine = StartWithEmptySnapshot();

        machine.Process(SnapshotForController(2, 5, "one", ControllerButton.South));
        machine.Process(SnapshotForController(3, 100, "two", ControllerButton.West));
        machine.Process(SnapshotForController(4, 350, "one", ControllerButton.South));
        machine.Process(SnapshotForController(5, 400, "one"));

        Assert.DoesNotContain(ControllerButton.West, machine.CapturedButtons);
        Assert.Equal(ControllerCapturePhase.Completed, machine.Phase);
    }

    [Fact]
    public void CancelEndsOnlyTheCaptureSession()
    {
        var machine = StartWithEmptySnapshot();
        machine.Cancel();

        Assert.Equal(ControllerCapturePhase.Cancelled, machine.Phase);
        Assert.Empty(machine.CapturedButtons);
    }

    [Fact]
    public void StartingAgainReplacesThePreviousSession()
    {
        var machine = StartWithEmptySnapshot();
        machine.Process(Snapshot(2, 5, ControllerButton.South));

        machine.Start(Snapshot(3, 100));
        machine.Process(Snapshot(4, 105, ControllerButton.West));

        Assert.Equal([ControllerButton.West], machine.CapturedButtons);
        Assert.DoesNotContain(ControllerButton.South, machine.CapturedButtons);
    }

    [Fact]
    public void TimesOutAndCanStartAnotherSession()
    {
        var machine = StartWithEmptySnapshot();
        machine.Process(Snapshot(2, 10_001, ControllerButton.South));
        Assert.Equal(ControllerCapturePhase.TimedOut, machine.Phase);

        machine.Start(Snapshot(3, 10_002));
        machine.Process(Snapshot(4, 10_010, ControllerButton.West));
        Assert.Equal([ControllerButton.West], machine.CapturedButtons);
    }

    [Fact]
    public void SingleQuickButtonIsCompletedAfterRelease()
    {
        var machine = StartWithEmptySnapshot();
        machine.Process(Snapshot(2, 5, ControllerButton.South));
        machine.Process(Snapshot(3, 100));

        Assert.Equal(ControllerCapturePhase.Completed, machine.Phase);
        Assert.Equal([ControllerButton.South], machine.CapturedButtons);
    }

    [Fact]
    public void CompletedResultRemainsAvailableUntilAnewSessionStarts()
    {
        var machine = StartWithEmptySnapshot();
        machine.Process(Snapshot(2, 5, ControllerButton.South, ControllerButton.West));
        machine.Process(Snapshot(3, 400));

        machine.Process(Snapshot(4, 500));

        Assert.Equal(ControllerCapturePhase.Completed, machine.Phase);
        Assert.Equal([ControllerButton.South, ControllerButton.West], machine.CapturedButtons.OrderBy(button => button));
    }

    private static ControllerCaptureStateMachine StartWithEmptySnapshot()
    {
        var machine = new ControllerCaptureStateMachine();
        machine.Start(Snapshot(1, 0));
        return machine;
    }

    private static ControllerInputSnapshot Snapshot(long sequence, int milliseconds, params ControllerButton[] buttons)
        => Snapshot(sequence, milliseconds, buttons, "one");

    private static ControllerInputSnapshot Snapshot(long sequence, int milliseconds, ControllerButton[] buttons, string controllerId)
        => new(controllerId, buttons.ToHashSet(), sequence, StartTime.AddMilliseconds(milliseconds));

    private static ControllerInputSnapshot SnapshotForController(long sequence, int milliseconds, string controllerId, params ControllerButton[] buttons)
        => Snapshot(sequence, milliseconds, buttons, controllerId);
}
