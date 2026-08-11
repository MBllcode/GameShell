using GameShell.Core.Models;
using GameShell.Core.Services;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class GamingProfileDisplayTopologyTests
{
    [Fact]
    public void LegacySnapshotMigratesAllEntriesIncludingDisabledDisplays()
    {
        var snapshot = Snapshot(
            Path("S90F", true, true, 0, 0, 3840, 2160, 144, 1),
            Path("PL3480WQ", false, false, -3440, 360, 3440, 1440, 0, 0));

        var topology = GamingProfileDisplayTopologyAdapter.FromLegacy(snapshot);

        Assert.Equal(2, topology.Displays.Count);
        Assert.True(topology.Displays[0].Enabled);
        Assert.False(topology.Displays[1].Enabled);
        Assert.Equal(-3440, topology.Displays[1].PositionX);
        Assert.Equal((uint)0, topology.Displays[1].RefreshRateNumerator);
        Assert.Equal((uint)0, topology.Displays[1].RefreshRateDenominator);
    }

    [Fact]
    public void TopologySupportsFiveDisplaysAndNegativePositions()
    {
        var topology = new GamingProfileDisplayTopology
        {
            Displays = Enumerable.Range(0, 5).Select(index => new ProfileDisplayTarget
            {
                Identity = Identity($"DISPLAY-{index}"),
                Enabled = true,
                IsPrimary = index == 0,
                PositionX = index == 0 ? 0 : -index * 1920,
                PositionY = index * 100,
                Width = 1920,
                Height = 1080,
                RefreshRateNumerator = 60,
                RefreshRateDenominator = 1
            }).ToList()
        };

        var snapshot = GamingProfileDisplayTopologyAdapter.ToLegacy(topology);

        Assert.Equal(5, snapshot.Displays.Count);
        Assert.Equal(-7680, snapshot.Displays[4].PositionX);
        Assert.True(snapshot.Displays.Single(display => display.IsPrimary).IsActive);
    }

    [Fact]
    public void AdapterPreservesDisabledStateAndUnknownRefresh()
    {
        var topology = new GamingProfileDisplayTopology
        {
            Displays =
            [
                new ProfileDisplayTarget
                {
                    Identity = Identity("S90F"),
                    Enabled = false,
                    IsPrimary = false,
                    Width = 3840,
                    Height = 2160,
                    RefreshRateNumerator = 0,
                    RefreshRateDenominator = 0
                }
            ]
        };

        var snapshot = GamingProfileDisplayTopologyAdapter.ToLegacy(topology);

        Assert.False(snapshot.Displays[0].IsActive);
        Assert.False(snapshot.Displays[0].IsPrimary);
        Assert.Equal((uint)0, snapshot.Displays[0].RefreshRateNumerator);
        Assert.Equal((uint)0, snapshot.Displays[0].RefreshRateDenominator);
    }

    [Fact]
    public void AdapterNormalizesPrimaryToOriginAndPreservesRelativePositions()
    {
        var topology = new GamingProfileDisplayTopology
        {
            Displays =
            [
                new ProfileDisplayTarget { Identity = Identity("DELL"), Enabled = true, IsPrimary = true, PositionX = 500, PositionY = 300, Width = 1920, Height = 1080 },
                new ProfileDisplayTarget { Identity = Identity("PL3480WQ"), Enabled = true, PositionX = -1420, PositionY = 500, Width = 3440, Height = 1440 }
            ]
        };

        var snapshot = GamingProfileDisplayTopologyAdapter.ToLegacy(topology);
        var primary = snapshot.Displays.Single(display => display.IsPrimary);
        var secondary = snapshot.Displays.Single(display => !display.IsPrimary);

        Assert.Equal(0, primary.PositionX);
        Assert.Equal(0, primary.PositionY);
        Assert.Equal(-1920, secondary.PositionX);
        Assert.Equal(200, secondary.PositionY);
    }

    [Fact]
    public void AdapterKeepsExactAdjacentCoordinatesForMixedDisplayWidths()
    {
        var topology = new GamingProfileDisplayTopology
        {
            Displays =
            [
                new ProfileDisplayTarget { Identity = Identity("DELL"), Enabled = true, IsPrimary = true, PositionX = 0, PositionY = 0, Width = 1920, Height = 1080 },
                new ProfileDisplayTarget { Identity = Identity("PL3480WQ"), Enabled = true, PositionX = 1920, PositionY = 0, Width = 3440, Height = 1440 }
            ]
        };

        var snapshot = GamingProfileDisplayTopologyAdapter.ToLegacy(topology);

        Assert.Equal(1920, snapshot.Displays.Single(display => display.Identity.FriendlyName == "PL3480WQ").PositionX);
        Assert.NotEqual(5360, snapshot.Displays.Single(display => display.Identity.FriendlyName == "PL3480WQ").PositionX);
        Assert.NotEqual(-5360, snapshot.Displays.Single(display => display.Identity.FriendlyName == "PL3480WQ").PositionX);
    }

    private static DisplayPathSnapshot Path(string name, bool active, bool primary, int x, int y, int width, int height, uint numerator, uint denominator)
    {
        return new DisplayPathSnapshot
        {
            Identity = Identity(name),
            IsActive = active,
            IsPrimary = primary,
            PositionX = x,
            PositionY = y,
            Width = width,
            Height = height,
            RefreshRateNumerator = numerator,
            RefreshRateDenominator = denominator
        };
    }

    private static DisplayLayoutSnapshot Snapshot(params DisplayPathSnapshot[] displays)
    {
        return new DisplayLayoutSnapshot { Displays = displays };
    }

    private static DisplayIdentity Identity(string name)
    {
        return new DisplayIdentity
        {
            MonitorDevicePath = $"\\\\?\\DISPLAY#{name}",
            FriendlyName = name,
            AdapterLuid = 1,
            TargetId = (uint)name.Length
        };
    }
}
