using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Displays;

public sealed class WindowsDisplayTopologyReader
{
    private const int MaxQueryAttempts = 3;
    private readonly ILogger<WindowsDisplayTopologyReader> _logger;

    public WindowsDisplayTopologyReader(ILogger<WindowsDisplayTopologyReader> logger)
    {
        _logger = logger;
    }

    internal NativeDisplayTopology QueryActive(CancellationToken cancellationToken)
    {
        return QueryTopology(DisplayConfigNative.QdcOnlyActivePaths, "active", cancellationToken);
    }

    internal NativeDisplayTopology QueryAvailable(CancellationToken cancellationToken)
    {
        return QueryTopology(DisplayConfigNative.QdcAllPaths, "available", cancellationToken);
    }

    internal NativeDisplayTopology QueryForLayoutApplication(CancellationToken cancellationToken)
    {
        try
        {
            var database = QueryTopology(DisplayConfigNative.QdcDatabaseCurrent, "database", cancellationToken);
            var available = QueryAvailable(cancellationToken);
            return MergeTopologies(database, available);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DisplayConfig database topology could not be read. Falling back to all paths.");
            return QueryAvailable(cancellationToken);
        }
    }

    private static NativeDisplayTopology MergeTopologies(NativeDisplayTopology primary, NativeDisplayTopology secondary)
    {
        var paths = primary.Paths
            .Concat(secondary.Paths)
            .GroupBy(path => $"{path.PathInfo.SourceInfo.AdapterId.ToInt64()}:{path.SourceId}:{path.PathInfo.TargetInfo.AdapterId.ToInt64()}:{path.TargetId}")
            .Select(group => group
                .OrderByDescending(path => path.IsActive)
                .ThenByDescending(path => path.IsAvailable)
                .ThenByDescending(path => path.TargetMode is not null)
                .ThenByDescending(GetTargetModeArea)
                .ThenByDescending(path => path.Width * path.Height)
                .First())
            .ToList();

        return new NativeDisplayTopology(paths, primary.PrimarySourceDeviceName, primary.CapturedAt);
    }

    private NativeDisplayTopology QueryTopology(uint queryFlags, string description, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reading {Description} display configuration through Windows DisplayConfig.", description);

        for (var attempt = 1; attempt <= MaxQueryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sizeResult = DisplayConfigNative.GetDisplayConfigBufferSizes(
                queryFlags,
                out var pathCount,
                out var modeCount);

            DisplayConfigNative.ThrowIfWin32Error(sizeResult, nameof(DisplayConfigNative.GetDisplayConfigBufferSizes));
            _logger.LogDebug("DisplayConfig buffer sizes. Paths: {PathCount}, Modes: {ModeCount}, Attempt: {Attempt}.", pathCount, modeCount, attempt);

            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];

            var queryPathCount = pathCount;
            var queryModeCount = modeCount;
            var queryResult = DisplayConfigNative.QueryDisplayConfig(
                queryFlags,
                ref queryPathCount,
                paths,
                ref queryModeCount,
                modes,
                IntPtr.Zero);

            if (queryResult == DisplayConfigNative.ErrorInsufficientBuffer)
            {
                _logger.LogWarning("Display configuration changed while reading buffers. Retrying DisplayConfig query. Attempt: {Attempt}.", attempt);
                continue;
            }

            if (queryResult != DisplayConfigNative.ErrorSuccess)
            {
                _logger.LogError("QueryDisplayConfig failed with Win32 error {ErrorCode}. Attempt: {Attempt}.", queryResult, attempt);
                DisplayConfigNative.ThrowIfWin32Error(queryResult, nameof(DisplayConfigNative.QueryDisplayConfig));
            }

            var queriedPaths = paths.Take((int)queryPathCount).ToArray();
            var queriedModes = modes.Take((int)queryModeCount).ToArray();
            _logger.LogInformation("QueryDisplayConfig returned {PathCount} {Description} path(s).", queriedPaths.Length, description);

            return new NativeDisplayTopology(
                queriedPaths.Select(path => MapNativePath(path, queriedModes)).ToList(),
                GetPrimarySourceDeviceName(),
                DateTimeOffset.UtcNow);
        }

        throw new InvalidOperationException("Display configuration changed during each DisplayConfig query attempt.");
    }

    private NativeDisplayPath MapNativePath(DisplayConfigPathInfo path, IReadOnlyList<DisplayConfigModeInfo> modes)
    {
        var sourceMode = ResolveSourceMode(path, modes);
        var targetMode = ResolveTargetMode(path, modes);
        var sourceName = TryGetSourceName(path.SourceInfo.AdapterId, path.SourceInfo.Id);
        var targetName = GetTargetName(path.TargetInfo.AdapterId, path.TargetInfo.Id);
        var preferredMode = TryGetTargetPreferredMode(path.TargetInfo.AdapterId, path.TargetInfo.Id);
        var effectiveTargetMode = targetMode ?? preferredMode?.TargetMode;
        var hasEdid = targetName.Flags.EdidIdsValid
            && targetName.EdidManufactureId != 0
            && targetName.EdidProductCodeId != 0;
        var isActive = (path.Flags & DisplayConfigNative.DisplayConfigPathActive) == DisplayConfigNative.DisplayConfigPathActive;
        var width = ResolveNativeWidth(isActive, sourceMode, effectiveTargetMode, preferredMode?.Width ?? 0);
        var height = ResolveNativeHeight(isActive, sourceMode, effectiveTargetMode, preferredMode?.Height ?? 0);

        if (!hasEdid)
        {
            _logger.LogDebug(
                "Display target does not expose complete EDID data. Source: {SourceDeviceName}, TargetId: {TargetId}, ConnectorInstance: {ConnectorInstance}.",
                sourceName,
                path.TargetInfo.Id,
                targetName.ConnectorInstance);
        }

        _logger.LogDebug(
            "DisplayConfig path mapped. Active: {IsActive}, Available: {IsAvailable}, Source: {SourceDeviceName}, FriendlyName: {FriendlyName}, Connection: {ConnectionTechnology}.",
            isActive,
            path.TargetInfo.TargetAvailable,
            sourceName,
            string.IsNullOrWhiteSpace(targetName.MonitorFriendlyDeviceName) ? "(unnamed)" : targetName.MonitorFriendlyDeviceName,
            FormatOutputTechnology(path.TargetInfo.OutputTechnology));

        var refreshRate = ResolveRefreshRate(path.TargetInfo.RefreshRate, effectiveTargetMode);
        return new NativeDisplayPath
        {
            SourceDeviceName = sourceName,
            MonitorFriendlyName = targetName.MonitorFriendlyDeviceName,
            MonitorDevicePath = targetName.MonitorDevicePath,
            HasEdid = hasEdid,
            EdidManufacturerId = targetName.EdidManufactureId,
            EdidProductCodeId = targetName.EdidProductCodeId,
            ConnectorInstance = targetName.ConnectorInstance,
            AdapterLuid = path.SourceInfo.AdapterId.ToInt64(),
            SourceId = path.SourceInfo.Id,
            TargetId = path.TargetInfo.Id,
            PositionX = sourceMode?.Position.X ?? 0,
            PositionY = sourceMode?.Position.Y ?? 0,
            Width = checked((int)width),
            Height = checked((int)height),
            Rotation = MapRotation(path.TargetInfo.Rotation),
            RefreshRateNumerator = refreshRate.Numerator,
            RefreshRateDenominator = refreshRate.Denominator,
            ConnectionTechnology = FormatOutputTechnology(path.TargetInfo.OutputTechnology),
            IsActive = isActive,
            IsAvailable = path.TargetInfo.TargetAvailable,
            PathInfo = path,
            SourceMode = sourceMode ?? new DisplayConfigSourceMode
            {
                Width = effectiveTargetMode?.TargetVideoSignalInfo.ActiveSize.Cx ?? preferredMode?.Width ?? 0,
                Height = effectiveTargetMode?.TargetVideoSignalInfo.ActiveSize.Cy ?? preferredMode?.Height ?? 0,
                PixelFormat = DisplayConfigPixelFormat.BitsPerPixel32,
                Position = new PointL()
            },
            TargetMode = effectiveTargetMode
        };
    }

    private static DisplayConfigRational ResolveRefreshRate(DisplayConfigRational pathRefreshRate, DisplayConfigTargetMode? targetMode)
    {
        if (pathRefreshRate.Numerator > 0 && pathRefreshRate.Denominator > 0)
        {
            return pathRefreshRate;
        }

        var targetRefreshRate = targetMode?.TargetVideoSignalInfo.VSyncFreq ?? default;
        return targetRefreshRate.Numerator > 0 && targetRefreshRate.Denominator > 0
            ? targetRefreshRate
            : default;
    }

    private static uint ResolveNativeWidth(bool isActive, DisplayConfigSourceMode? sourceMode, DisplayConfigTargetMode? targetMode, uint preferredWidth)
    {
        var sourceWidth = sourceMode?.Width ?? 0;
        var targetWidth = targetMode?.TargetVideoSignalInfo.ActiveSize.Cx ?? 0;
        var inactiveWidth = preferredWidth > 0 ? preferredWidth : targetWidth;
        return !isActive && inactiveWidth > 0
            ? inactiveWidth
            : sourceWidth > 0 ? sourceWidth : targetWidth;
    }

    private static uint ResolveNativeHeight(bool isActive, DisplayConfigSourceMode? sourceMode, DisplayConfigTargetMode? targetMode, uint preferredHeight)
    {
        var sourceHeight = sourceMode?.Height ?? 0;
        var targetHeight = targetMode?.TargetVideoSignalInfo.ActiveSize.Cy ?? 0;
        var inactiveHeight = preferredHeight > 0 ? preferredHeight : targetHeight;
        return !isActive && inactiveHeight > 0
            ? inactiveHeight
            : sourceHeight > 0 ? sourceHeight : targetHeight;
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

    private static DisplayConfigSourceMode? ResolveSourceMode(DisplayConfigPathInfo path, IReadOnlyList<DisplayConfigModeInfo> modes)
    {
        if (path.SourceInfo.ModeInfoIdx < modes.Count)
        {
            var mode = modes[(int)path.SourceInfo.ModeInfoIdx];
            if (mode.InfoType == DisplayConfigModeInfoType.Source)
            {
                return mode.ModeInfo.SourceMode;
            }
        }

        var fallback = modes.FirstOrDefault(mode =>
            mode.InfoType == DisplayConfigModeInfoType.Source
            && mode.Id == path.SourceInfo.Id
            && mode.AdapterId.ToInt64() == path.SourceInfo.AdapterId.ToInt64());

        return fallback.InfoType == DisplayConfigModeInfoType.Source
            ? fallback.ModeInfo.SourceMode
            : null;
    }

    private static DisplayConfigTargetMode? ResolveTargetMode(DisplayConfigPathInfo path, IReadOnlyList<DisplayConfigModeInfo> modes)
    {
        if (path.TargetInfo.ModeInfoIdx < modes.Count)
        {
            var mode = modes[(int)path.TargetInfo.ModeInfoIdx];
            if (mode.InfoType == DisplayConfigModeInfoType.Target)
            {
                return mode.ModeInfo.TargetMode;
            }
        }

        var fallback = modes.FirstOrDefault(mode =>
            mode.InfoType == DisplayConfigModeInfoType.Target
            && mode.Id == path.TargetInfo.Id
            && mode.AdapterId.ToInt64() == path.TargetInfo.AdapterId.ToInt64());

        return fallback.InfoType == DisplayConfigModeInfoType.Target
            ? fallback.ModeInfo.TargetMode
            : null;
    }

    private static string TryGetSourceName(Luid adapterId, uint sourceId)
    {
        var request = new DisplayConfigSourceDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfigDeviceInfoType.GetSourceName,
                Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                AdapterId = adapterId,
                Id = sourceId
            },
            ViewGdiDeviceName = string.Empty
        };

        var result = DisplayConfigNative.DisplayConfigGetDeviceInfo(ref request);
        return result == DisplayConfigNative.ErrorSuccess
            ? request.ViewGdiDeviceName
            : string.Empty;
    }

    private static DisplayConfigTargetDeviceName GetTargetName(Luid adapterId, uint targetId)
    {
        var request = new DisplayConfigTargetDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfigDeviceInfoType.GetTargetName,
                Size = (uint)Marshal.SizeOf<DisplayConfigTargetDeviceName>(),
                AdapterId = adapterId,
                Id = targetId
            },
            MonitorFriendlyDeviceName = string.Empty,
            MonitorDevicePath = string.Empty
        };

        var result = DisplayConfigNative.DisplayConfigGetDeviceInfo(ref request);
        DisplayConfigNative.ThrowIfWin32Error(result, "DisplayConfigGetDeviceInfo(TargetName)");
        return request;
    }

    private DisplayConfigTargetPreferredMode? TryGetTargetPreferredMode(Luid adapterId, uint targetId)
    {
        var request = new DisplayConfigTargetPreferredMode
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfigDeviceInfoType.GetTargetPreferredMode,
                Size = (uint)Marshal.SizeOf<DisplayConfigTargetPreferredMode>(),
                AdapterId = adapterId,
                Id = targetId
            }
        };

        var result = DisplayConfigNative.DisplayConfigGetDeviceInfo(ref request);
        if (result != DisplayConfigNative.ErrorSuccess)
        {
            _logger.LogDebug(
                "DisplayConfigGetDeviceInfo(TargetPreferredMode) failed with Win32 error {ErrorCode}. TargetId: {TargetId}.",
                result,
                targetId);
            return null;
        }

        return request.Width > 0 && request.Height > 0
            ? request
            : null;
    }

    private static NativeDisplayRotation MapRotation(DisplayConfigRotation rotation)
    {
        return rotation switch
        {
            DisplayConfigRotation.Identity => NativeDisplayRotation.Identity,
            DisplayConfigRotation.Rotate90 => NativeDisplayRotation.Rotate90,
            DisplayConfigRotation.Rotate180 => NativeDisplayRotation.Rotate180,
            DisplayConfigRotation.Rotate270 => NativeDisplayRotation.Rotate270,
            _ => NativeDisplayRotation.Unknown
        };
    }

    private static string FormatOutputTechnology(DisplayConfigVideoOutputTechnology technology)
    {
        return technology switch
        {
            DisplayConfigVideoOutputTechnology.Hd15 => "VGA",
            DisplayConfigVideoOutputTechnology.Dvi => "DVI",
            DisplayConfigVideoOutputTechnology.Hdmi => "HDMI",
            DisplayConfigVideoOutputTechnology.DisplayPortExternal => "DisplayPort",
            DisplayConfigVideoOutputTechnology.DisplayPortEmbedded => "Embedded DisplayPort",
            DisplayConfigVideoOutputTechnology.Internal => "Internal",
            DisplayConfigVideoOutputTechnology.Miracast => "Miracast",
            DisplayConfigVideoOutputTechnology.IndirectWired => "Indirect wired",
            DisplayConfigVideoOutputTechnology.Lvds => "LVDS",
            DisplayConfigVideoOutputTechnology.Other => "Other",
            _ => technology.ToString()
        };
    }

    private static string GetPrimarySourceDeviceName()
    {
        for (uint index = 0; ; index++)
        {
            var displayDevice = new DisplayDevice
            {
                Cb = (uint)Marshal.SizeOf<DisplayDevice>(),
                DeviceName = string.Empty,
                DeviceString = string.Empty,
                DeviceId = string.Empty,
                DeviceKey = string.Empty
            };

            if (!DisplayConfigNative.EnumDisplayDevices(null, index, ref displayDevice, 0))
            {
                break;
            }

            if ((displayDevice.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) == DisplayDeviceStateFlags.PrimaryDevice)
            {
                return displayDevice.DeviceName;
            }
        }

        return string.Empty;
    }
}
