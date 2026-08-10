using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Displays;

internal sealed class DisplayConfigMapper
{
    private readonly ILogger _logger;

    public DisplayConfigMapper(ILogger logger)
    {
        _logger = logger;
    }

    public DisplayLayoutSnapshot MapSnapshot(NativeDisplayTopology topology)
    {
        var primarySourceName = !string.IsNullOrWhiteSpace(topology.PrimarySourceDeviceName)
            ? topology.PrimarySourceDeviceName
            : ResolvePrimarySourceNameByOrigin(topology.Paths);
        if (string.IsNullOrWhiteSpace(primarySourceName) && topology.Paths.Count > 0)
        {
            _logger.LogWarning("DisplayConfig snapshot did not contain a display that could be confidently marked as primary.");
        }

        var displays = DeduplicateDisplayPaths(topology.Paths)
            .Select(path => MapDisplayPath(path, primarySourceName))
            .ToList();

        return new DisplayLayoutSnapshot
        {
            CapturedAt = topology.CapturedAt,
            Displays = displays
        };
    }

    public IReadOnlyList<DisplayDeviceInfo> MapDevices(NativeDisplayTopology topology)
    {
        var snapshot = MapSnapshot(topology);
        return snapshot.Displays
            .Select(display => new DisplayDeviceInfo
            {
                Id = display.Identity.MonitorDevicePath,
                Name = display.Identity.FriendlyName,
                IsPrimary = display.IsPrimary,
                IsEnabled = display.IsActive,
                Identity = display.Identity,
                SourceDeviceName = display.SourceDeviceName,
                SourceId = display.SourceId,
                TargetId = display.TargetId,
                AdapterLuid = display.AdapterLuid,
                ConnectionTechnology = display.ConnectionTechnology,
                PositionX = display.PositionX,
                PositionY = display.PositionY,
                Width = display.Width,
                Height = display.Height,
                Orientation = display.Orientation,
                RefreshRateNumerator = display.RefreshRateNumerator,
                RefreshRateDenominator = display.RefreshRateDenominator
            })
            .ToList();
    }

    private DisplayPathSnapshot MapDisplayPath(NativeDisplayPath path, string primarySourceName)
    {
        var identity = new DisplayIdentity
        {
            MonitorDevicePath = path.MonitorDevicePath,
            FriendlyName = path.MonitorFriendlyName,
            EdidManufacturerId = path.EdidManufacturerId,
            EdidProductCodeId = path.EdidProductCodeId,
            ConnectorInstance = path.ConnectorInstance,
            AdapterLuid = path.AdapterLuid,
            TargetId = path.TargetId
        };

        if (!path.HasEdid)
        {
            _logger.LogDebug(
                "Display {SourceDeviceName} did not expose EDID identifiers. TargetId: {TargetId}, ConnectorInstance: {ConnectorInstance}",
                path.SourceDeviceName,
                path.TargetId,
                path.ConnectorInstance);
        }

        return new DisplayPathSnapshot
        {
            Identity = identity,
            IsActive = path.IsActive,
            IsPrimary = !string.IsNullOrWhiteSpace(primarySourceName)
                && string.Equals(path.SourceDeviceName, primarySourceName, StringComparison.OrdinalIgnoreCase),
            PositionX = path.PositionX,
            PositionY = path.PositionY,
            Width = path.Width,
            Height = path.Height,
            NativeWidth = checked((int)path.PreferredWidth),
            NativeHeight = checked((int)path.PreferredHeight),
            Orientation = MapOrientation(path.Rotation),
            RefreshRateNumerator = path.RefreshRateNumerator,
            RefreshRateDenominator = path.RefreshRateDenominator,
            SourceId = path.SourceId,
            TargetId = path.TargetId,
            AdapterLuid = path.AdapterLuid,
            SourceDeviceName = path.SourceDeviceName,
            ConnectionTechnology = path.ConnectionTechnology
        };
    }

    private static IReadOnlyList<NativeDisplayPath> DeduplicateDisplayPaths(IReadOnlyList<NativeDisplayPath> paths)
    {
        return paths
            .Where(path => path.IsAvailable || path.IsActive)
            .GroupBy(path => !string.IsNullOrWhiteSpace(path.MonitorDevicePath)
                ? path.MonitorDevicePath
                : $"{path.AdapterLuid}:{path.TargetId}:{path.MonitorFriendlyName}")
            .Select(group => group
                .OrderByDescending(path => path.IsActive)
                .ThenByDescending(path => path.TargetMode is not null)
                .ThenByDescending(GetTargetModeArea)
                .ThenByDescending(path => path.Width * path.Height)
                .First())
            .ToList();
    }

    private static ulong GetTargetModeArea(NativeDisplayPath path)
    {
        if (path.TargetMode is not { } targetMode)
        {
            return 0;
        }

        return (ulong)targetMode.TargetVideoSignalInfo.ActiveSize.Cx
            * targetMode.TargetVideoSignalInfo.ActiveSize.Cy;
    }

    private static string ResolvePrimarySourceNameByOrigin(IReadOnlyList<NativeDisplayPath> paths)
    {
        var originDisplays = paths
            .Where(path => path.PositionX == 0 && path.PositionY == 0)
            .ToList();

        return originDisplays.Count == 1
            ? originDisplays[0].SourceDeviceName
            : string.Empty;
    }

    private static DisplayOrientation MapOrientation(NativeDisplayRotation rotation)
    {
        return rotation switch
        {
            NativeDisplayRotation.Identity => DisplayOrientation.Identity,
            NativeDisplayRotation.Rotate90 => DisplayOrientation.Rotate90,
            NativeDisplayRotation.Rotate180 => DisplayOrientation.Rotate180,
            NativeDisplayRotation.Rotate270 => DisplayOrientation.Rotate270,
            _ => DisplayOrientation.Unknown
        };
    }
}
