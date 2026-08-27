using GameShell.Windows.Services;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class WindowPlacementGeometryTests
{
    private static readonly PixelWorkArea Primary = new("DISPLAY1", 0, 0, 1920, 1040, true);
    private static readonly PixelWorkArea Left = new("DISPLAY2", -2560, 0, 2560, 1440, false);

    [Fact]
    public void FullyVisibleWindowRemainsValid()
    {
        var window = new PixelRect(200, 100, 1200, 800);

        Assert.True(WindowPlacementCalculator.IsMeaningfullyVisible(window, [Primary]));
    }

    [Fact]
    public void NegativeCoordinateWindowOnActiveLeftMonitorRemainsValid()
    {
        var window = new PixelRect(-2200, 100, 1200, 800);

        Assert.True(WindowPlacementCalculator.IsMeaningfullyVisible(window, [Primary, Left]));
    }

    [Fact]
    public void TinySliverDoesNotCountAsReachable()
    {
        var window = new PixelRect(1900, 100, 1200, 800);

        Assert.False(WindowPlacementCalculator.IsMeaningfullyVisible(window, [Primary]));
    }

    [Fact]
    public void DisconnectedWindowIsCenteredOnCurrentPrimary()
    {
        var window = new PixelRect(-3840, 100, 1200, 800);
        var recovery = WindowPlacementCalculator.SelectRecoveryMonitor([Primary, Left], "DISPLAY2");

        Assert.Equal("DISPLAY1", recovery?.DeviceName);
        Assert.Equal(new PixelRect(360, 120, 1200, 800),
            WindowPlacementCalculator.CenterAndClamp(window, recovery!.Value));
    }

    [Fact]
    public void OversizedWindowIsClampedInsideWorkArea()
    {
        var window = new PixelRect(-100, -100, 3000, 1600);

        Assert.Equal(new PixelRect(0, 0, 1920, 1040),
            WindowPlacementCalculator.CenterAndClamp(window, Primary));
    }

    [Fact]
    public void ConfiguredWorkPrimaryIsFallbackWhenNoCurrentPrimaryIsReported()
    {
        var monitors = new[]
        {
            new PixelWorkArea("DISPLAY2", -2560, 0, 2560, 1440, false),
            new PixelWorkArea("DISPLAY3", 0, 0, 1920, 1040, false)
        };

        var recovery = WindowPlacementCalculator.SelectRecoveryMonitor(monitors, "DISPLAY3");

        Assert.Equal("DISPLAY3", recovery?.DeviceName);
    }

    [Fact]
    public void OneHundredPercentDpiRoundTripsWithoutScaling()
    {
        var dips = new DipRect(33, 0, 1575, 900);

        var pixels = DpiCoordinateConverter.ToPixels(dips, DpiScaleFactor.Identity);

        Assert.Equal(new PixelRect(33, 0, 1575, 900), pixels);
        Assert.Equal(dips, DpiCoordinateConverter.ToDips(pixels, DpiScaleFactor.Identity));
    }

    [Fact]
    public void TwoHundredFortyPercentDpiConvertsOnceAndRoundTrips()
    {
        var dips = new DipRect(0, 0, 1575, 900);
        var scale = new DpiScaleFactor(2.4, 2.4);

        var pixels = DpiCoordinateConverter.ToPixels(dips, scale);

        Assert.Equal(new PixelRect(0, 0, 3780, 2160), pixels);
        var roundTrip = DpiCoordinateConverter.ToDips(pixels, scale);
        Assert.Equal(1575, roundTrip.Width, 6);
        Assert.Equal(900, roundTrip.Height, 6);
    }

    [Fact]
    public void OneHundredFiftyPercentDpiConvertsPhysicalWorkAreaOnce()
    {
        var dips = new DipRect(0, 0, 1920, 1040);

        var pixels = DpiCoordinateConverter.ToPixels(dips, new DpiScaleFactor(1.5, 1.5));

        Assert.Equal(new PixelRect(0, 0, 2880, 1560), pixels);
    }

    [Fact]
    public void MixedDpiScalesConvertAxesIndependently()
    {
        var dips = new DipRect(-100.5, 20.25, 1575, 900);
        var scale = new DpiScaleFactor(1.0, 2.4);

        var pixels = DpiCoordinateConverter.ToPixels(dips, scale);

        Assert.Equal(new PixelRect(-101, 49, 1575, 2160), pixels);
    }

    [Fact]
    public void ClearlyOversizedLegacyDipBoundsAreRejected()
    {
        Assert.True(WindowPlacementCalculator.IsClearlyCorruptSavedBounds(
            new DipRect(2888, 269, 3780, 2700),
            new DipRect(0, 0, 1260, 900)));
    }

    [Fact]
    public void NormalCustomDipBoundsArePreserved()
    {
        Assert.False(WindowPlacementCalculator.IsClearlyCorruptSavedBounds(
            new DipRect(-2200, 100, 1575, 900),
            new DipRect(0, 0, 1260, 900)));
    }

    [Fact]
    public void MinimizedWindowUsesNormalRestoreRectForValidation()
    {
        var current = new PixelRect(-32000, -32000, 468, 72);
        var normal = new PixelRect(100, 80, 1575, 900);

        Assert.Equal(normal, WindowPlacementCalculator.SelectValidationRect(current, normal, true));
    }
}
