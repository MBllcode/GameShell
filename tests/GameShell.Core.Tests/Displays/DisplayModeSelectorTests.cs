using GameShell.Windows.Displays;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class DisplayModeSelectorTests
{
    [Fact]
    public void SelectHighestRefreshMode_SelectsHighestRefreshForMatchingResolution()
    {
        var modes = new[]
        {
            CreateMode(3840, 2160, 60),
            CreateMode(3440, 1440, 240),
            CreateMode(3840, 2160, 120),
            CreateMode(3840, 2160, 144)
        };

        var selected = DisplayModeSelector.SelectHighestRefreshMode(modes, 3840, 2160);

        Assert.NotNull(selected);
        Assert.Equal(3840u, selected.Value.PelsWidth);
        Assert.Equal(2160u, selected.Value.PelsHeight);
        Assert.Equal(144u, selected.Value.DisplayFrequency);
    }

    [Fact]
    public void SelectHighestRefreshMode_ReturnsNullWhenResolutionIsUnavailable()
    {
        var modes = new[]
        {
            CreateMode(2560, 1440, 144),
            CreateMode(1920, 1080, 240)
        };

        var selected = DisplayModeSelector.SelectHighestRefreshMode(modes, 3840, 2160);

        Assert.Null(selected);
    }

    private static DevMode CreateMode(uint width, uint height, uint refreshRate)
    {
        var mode = DevMode.Create();
        mode.PelsWidth = width;
        mode.PelsHeight = height;
        mode.DisplayFrequency = refreshRate;
        mode.BitsPerPel = 32;
        return mode;
    }
}
