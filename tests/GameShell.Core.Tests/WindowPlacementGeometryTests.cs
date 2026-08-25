using GameShell.Windows.Services;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class WindowPlacementGeometryTests
{
    private static readonly MonitorWorkArea Primary = new("DISPLAY1", 0, 0, 1920, 1040, true);
    private static readonly MonitorWorkArea Left = new("DISPLAY2", -2560, 0, 2560, 1440, false);

    [Fact]
    public void FullyVisibleWindowRemainsValid()
    {
        var window = new WindowPlacementRect(200, 100, 1200, 800);

        Assert.True(WindowPlacementCalculator.IsMeaningfullyVisible(window, [Primary]));
    }

    [Fact]
    public void NegativeCoordinateWindowOnActiveLeftMonitorRemainsValid()
    {
        var window = new WindowPlacementRect(-2200, 100, 1200, 800);

        Assert.True(WindowPlacementCalculator.IsMeaningfullyVisible(window, [Primary, Left]));
    }

    [Fact]
    public void TinySliverDoesNotCountAsReachable()
    {
        var window = new WindowPlacementRect(1900, 100, 1200, 800);

        Assert.False(WindowPlacementCalculator.IsMeaningfullyVisible(window, [Primary]));
    }

    [Fact]
    public void DisconnectedWindowIsCenteredOnCurrentPrimary()
    {
        var window = new WindowPlacementRect(-3840, 100, 1200, 800);
        var recovery = WindowPlacementCalculator.SelectRecoveryMonitor([Primary, Left], "DISPLAY2");

        Assert.Equal("DISPLAY1", recovery?.DeviceName);
        Assert.Equal(new WindowPlacementRect(360, 120, 1200, 800),
            WindowPlacementCalculator.CenterAndClamp(window, recovery!.Value));
    }

    [Fact]
    public void OversizedWindowIsClampedInsideWorkArea()
    {
        var window = new WindowPlacementRect(-100, -100, 3000, 1600);

        Assert.Equal(new WindowPlacementRect(0, 0, 1920, 1040),
            WindowPlacementCalculator.CenterAndClamp(window, Primary));
    }

    [Fact]
    public void ConfiguredWorkPrimaryIsFallbackWhenNoCurrentPrimaryIsReported()
    {
        var monitors = new[]
        {
            new MonitorWorkArea("DISPLAY2", -2560, 0, 2560, 1440, false),
            new MonitorWorkArea("DISPLAY3", 0, 0, 1920, 1040, false)
        };

        var recovery = WindowPlacementCalculator.SelectRecoveryMonitor(monitors, "DISPLAY3");

        Assert.Equal("DISPLAY3", recovery?.DeviceName);
    }
}
