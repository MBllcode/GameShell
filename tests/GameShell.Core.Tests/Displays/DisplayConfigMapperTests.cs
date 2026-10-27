using GameShell.Core.Displays;
using System.Runtime.InteropServices;
using GameShell.Windows.Displays;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class DisplayConfigMapperTests
{
    [Fact]
    public void DisplayConfigNativeStructs_MatchWindowsX64Layout()
    {
        Assert.Equal(20, Marshal.SizeOf<DisplayConfigPathSourceInfo>());
        Assert.Equal(48, Marshal.SizeOf<DisplayConfigPathTargetInfo>());
        Assert.Equal(72, Marshal.SizeOf<DisplayConfigPathInfo>());
    }

    [Fact]
    public void MapSnapshot_MapsSingleActiveDisplay()
    {
        var snapshot = Map(CreatePath(
            name: "Samsung OLED S90F",
            path: @"\\?\DISPLAY#SAM1234",
            source: @"\\.\DISPLAY1",
            x: 0,
            y: 0,
            width: 3840,
            height: 2160,
            refreshNumerator: 144000,
            refreshDenominator: 1000));

        var display = Assert.Single(snapshot.Displays);
        Assert.Equal("Samsung OLED S90F", display.Identity.FriendlyName);
        Assert.True(display.IsActive);
        Assert.True(display.IsPrimary);
        Assert.Equal(3840, display.Width);
        Assert.Equal(2160, display.Height);
        Assert.Equal(144000u, display.RefreshRateNumerator);
        Assert.Equal(1000u, display.RefreshRateDenominator);
        Assert.Equal(144, display.RefreshRateHz);
    }

    [Fact]
    public void MapSnapshot_MapsMultipleDisplaysWithPositionsAndResolution()
    {
        var snapshot = Map(
            CreatePath("LG UltraGear", @"\\?\DISPLAY#LG111", @"\\.\DISPLAY2", -3440, 0, 3440, 1440),
            CreatePath("Samsung OLED", @"\\?\DISPLAY#SAM222", @"\\.\DISPLAY1", 0, 0, 3840, 2160),
            CreatePath("Dell", @"\\?\DISPLAY#DEL333", @"\\.\DISPLAY3", 3840, 0, 2560, 1440));

        Assert.Equal(3, snapshot.Displays.Count);
        Assert.Contains(snapshot.Displays, display => display.PositionX == -3440 && display.Width == 3440);
        Assert.Contains(snapshot.Displays, display => display.PositionX == 3840 && display.Width == 2560);
        Assert.Single(snapshot.Displays, display => display.IsPrimary);
    }

    [Fact]
    public void MapSnapshot_PreservesOrientation()
    {
        var snapshot = Map(CreatePath(
            "Portrait",
            @"\\?\DISPLAY#PORTRAIT",
            @"\\.\DISPLAY1",
            0,
            0,
            1440,
            2560,
            rotation: NativeDisplayRotation.Rotate90));

        Assert.Equal(DisplayOrientation.Rotate90, snapshot.Displays[0].Orientation);
    }

    [Fact]
    public void MapSnapshot_AllowsMissingEdidData()
    {
        var snapshot = Map(CreatePath(
            "Generic Monitor",
            @"\\?\DISPLAY#GENERIC",
            @"\\.\DISPLAY1",
            0,
            0,
            1920,
            1080,
            edidManufacturer: 0,
            edidProduct: 0,
            hasEdid: false));

        var display = snapshot.Displays[0];
        Assert.Equal(0u, display.Identity.EdidManufacturerId);
        Assert.Equal(0u, display.Identity.EdidProductCodeId);
        Assert.True(display.IsPrimary);
    }

    [Fact]
    public void MapSnapshot_DoesNotGuessPrimaryWhenOriginIsAmbiguous()
    {
        var snapshot = Map(
            CreatePath("Display A", @"\\?\DISPLAY#A", @"\\.\DISPLAY1", 0, 0, 1920, 1080),
            CreatePath("Display B", @"\\?\DISPLAY#B", @"\\.\DISPLAY2", 0, 0, 1920, 1080));

        Assert.DoesNotContain(snapshot.Displays, display => display.IsPrimary);
    }

    [Fact]
    public void MapSnapshot_UsesPrimarySourceDeviceNameBeforeOriginFallback()
    {
        var mapper = new DisplayConfigMapper(NullLogger.Instance);
        var snapshot = mapper.MapSnapshot(new NativeDisplayTopology(
            [
                CreatePath("Display A", @"\\?\DISPLAY#A", @"\\.\DISPLAY1", 0, 0, 1920, 1080),
                CreatePath("Display B", @"\\?\DISPLAY#B", @"\\.\DISPLAY2", 1920, 0, 1920, 1080)
            ],
            @"\\.\DISPLAY2",
            DateTimeOffset.UnixEpoch));

        Assert.True(snapshot.Displays.Single(display => display.SourceDeviceName == @"\\.\DISPLAY2").IsPrimary);
        Assert.False(snapshot.Displays.Single(display => display.SourceDeviceName == @"\\.\DISPLAY1").IsPrimary);
    }

    [Fact]
    public void MapSnapshot_PrefersTargetModeResolutionWhenDeduplicatingInactiveDisplay()
    {
        var snapshot = Map(
            CreatePath(
                "Samsung OLED",
                @"\\?\DISPLAY#SAM222",
                @"\\.\DISPLAY3",
                0,
                0,
                3440,
                1440,
                isActive: false,
                targetWidth: 3440,
                targetHeight: 1440),
            CreatePath(
                "Samsung OLED",
                @"\\?\DISPLAY#SAM222",
                @"\\.\DISPLAY4",
                0,
                0,
                3840,
                2160,
                isActive: false,
                targetWidth: 3840,
                targetHeight: 2160));

        var display = Assert.Single(snapshot.Displays);
        Assert.False(display.IsActive);
        Assert.Equal(3840, display.Width);
        Assert.Equal(2160, display.Height);
    }

    [Fact]
    public void MapSnapshot_PrefersActiveSourceForDuplicatePhysicalTarget()
    {
        var snapshot = Map(
            CreatePath("S90F", @"\\?\DISPLAY#SAM222", @"\\.\DISPLAY9", 0, 0, 3840, 2160, isActive: false, targetWidth: 3840, targetHeight: 2160),
            CreatePath("S90F", @"\\?\DISPLAY#SAM222", @"\\.\DISPLAY1", 0, 0, 3840, 2160, isActive: true));

        var display = Assert.Single(snapshot.Displays);
        Assert.True(display.IsActive);
        Assert.Equal(@"\\.\DISPLAY1", display.SourceDeviceName);
        Assert.Equal(3840, display.Width);
        Assert.Equal(2160, display.Height);
    }

    [Fact]
    public void IdentityMatcher_reports_conflicting_edid_even_when_monitor_path_matches()
    {
        var saved = Identity(path: "PATH-A", name: "Old Name", manufacturer: 1, product: 2, connector: 3);
        var detected = Identity(path: "PATH-A", name: "New Name", manufacturer: 9, product: 9, connector: 9);

        var confidence = DisplayIdentityMatcher.GetMatchConfidence(saved, detected);

        Assert.Equal(DisplayIdentityMatchConfidence.IdentityConflict, confidence);
        Assert.False(DisplayIdentityMatcher.IsReliableMatch(saved, detected));
    }

    [Fact]
    public void IdentityMatcher_does_not_report_conflict_for_a_different_route()
    {
        var saved = Identity(path: "PATH-A", name: "Monitor A", manufacturer: 1, product: 2, connector: 3) with
        {
            AdapterLuid = 10,
            TargetId = 1
        };
        var detected = Identity(path: "PATH-B", name: "Monitor B", manufacturer: 9, product: 9, connector: 4) with
        {
            AdapterLuid = 10,
            TargetId = 2
        };

        Assert.Equal(DisplayIdentityMatchConfidence.None, DisplayIdentityMatcher.GetMatchConfidence(saved, detected));
        Assert.False(DisplayIdentityMatcher.IsSamePhysicalDisplay(saved, detected));
    }

    [Fact]
    public void IdentityMatcher_FallsBackToEdidProductAndConnector()
    {
        var saved = Identity(path: "", name: "Monitor", manufacturer: 123, product: 456, connector: 7);
        var detected = Identity(path: "", name: "Renamed", manufacturer: 123, product: 456, connector: 7);

        Assert.Equal(
            DisplayIdentityMatchConfidence.EdidProductAndConnector,
            DisplayIdentityMatcher.GetMatchConfidence(saved, detected));
    }

    [Fact]
    public void IdentityMatcher_FallsBackToEdidProductWithoutConnector()
    {
        var saved = Identity(path: "", name: "Monitor", manufacturer: 123, product: 456, connector: 0);
        var detected = Identity(path: "", name: "Renamed", manufacturer: 123, product: 456, connector: 9);

        Assert.Equal(DisplayIdentityMatchConfidence.EdidProduct, DisplayIdentityMatcher.GetMatchConfidence(saved, detected));
    }

    [Fact]
    public void IdentityMatcher_DoesNotTreatFriendlyNameAsReliable()
    {
        var saved = Identity(path: "", name: "Generic PnP Monitor", manufacturer: 0, product: 0, connector: 0);
        var detected = Identity(path: "", name: "Generic PnP Monitor", manufacturer: 0, product: 0, connector: 0);

        Assert.Equal(DisplayIdentityMatchConfidence.WeakFriendlyName, DisplayIdentityMatcher.GetMatchConfidence(saved, detected));
        Assert.False(DisplayIdentityMatcher.IsReliableMatch(saved, detected));
    }

    private static DisplayLayoutSnapshot Map(params NativeDisplayPath[] paths)
    {
        var mapper = new DisplayConfigMapper(NullLogger.Instance);
        return mapper.MapSnapshot(new NativeDisplayTopology(paths, string.Empty, DateTimeOffset.UnixEpoch));
    }

    private static NativeDisplayPath CreatePath(
        string name,
        string path,
        string source,
        int x,
        int y,
        int width,
        int height,
        uint refreshNumerator = 60000,
        uint refreshDenominator = 1000,
        NativeDisplayRotation rotation = NativeDisplayRotation.Identity,
        uint edidManufacturer = 111,
        uint edidProduct = 222,
        bool hasEdid = true,
        bool isActive = true,
        int? targetWidth = null,
        int? targetHeight = null)
    {
        return new NativeDisplayPath
        {
            MonitorFriendlyName = name,
            MonitorDevicePath = path,
            SourceDeviceName = source,
            PositionX = x,
            PositionY = y,
            Width = width,
            Height = height,
            RefreshRateNumerator = refreshNumerator,
            RefreshRateDenominator = refreshDenominator,
            Rotation = rotation,
            EdidManufacturerId = edidManufacturer,
            EdidProductCodeId = edidProduct,
            ConnectorInstance = 1,
            AdapterLuid = 42,
            SourceId = 3,
            TargetId = 4,
            ConnectionTechnology = "HDMI",
            IsActive = isActive,
            HasEdid = hasEdid,
            TargetMode = targetWidth.HasValue && targetHeight.HasValue
                ? new DisplayConfigTargetMode
                {
                    TargetVideoSignalInfo = new DisplayConfigVideoSignalInfo
                    {
                        ActiveSize = new DisplayConfig2DRegion
                        {
                            Cx = checked((uint)targetWidth.Value),
                            Cy = checked((uint)targetHeight.Value)
                        }
                    }
                }
                : null
        };
    }

    private static DisplayIdentity Identity(string path, string name, uint manufacturer, uint product, uint connector)
    {
        return new DisplayIdentity
        {
            MonitorDevicePath = path,
            FriendlyName = name,
            EdidIdsValid = manufacturer != 0 && product != 0,
            EdidManufacturerId = manufacturer,
            EdidProductCodeId = product,
            ConnectorInstance = connector
        };
    }
}
