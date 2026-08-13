namespace GameShell.Core.Models;

public enum DisplayOrientation
{
    Identity = 0,
    Rotate90 = 1,
    Rotate180 = 2,
    Rotate270 = 3,
    Unknown = 255
}

public enum DisplayIdentityMatchConfidence
{
    None,
    WeakFriendlyName,
    EdidProduct,
    EdidProductAndConnector,
    AdapterAndTargetRoute,
    ExactMonitorDevicePath
}

public sealed record DisplayIdentity
{
    public string MonitorDevicePath { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public bool EdidIdsValid { get; init; }
    public uint EdidManufacturerId { get; init; }
    public uint EdidProductCodeId { get; init; }
    public uint ConnectorInstance { get; init; }
    public string OutputTechnology { get; init; } = string.Empty;
    public long AdapterLuid { get; init; }
    public uint TargetId { get; init; }
}

public sealed class DisplayModeInfo
{
    public int Width { get; init; }
    public int Height { get; init; }
    public uint RefreshRateNumerator { get; init; }
    public uint RefreshRateDenominator { get; init; }

    public double RefreshRateHz => RefreshRateDenominator == 0
        ? 0
        : (double)RefreshRateNumerator / RefreshRateDenominator;
}

public sealed class DisplayModeQueryResult
{
    public bool Success { get; init; }
    public bool InventoryValid { get; init; } = true;
    public IReadOnlyList<DisplayModeInfo> Modes { get; init; } = [];
}

public sealed class DisplayDeviceInfo
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsAvailable { get; init; }
    public DisplayIdentity Identity { get; init; } = new();
    public string SourceDeviceName { get; init; } = string.Empty;
    public uint SourceId { get; init; }
    public uint TargetId { get; init; }
    public long AdapterLuid { get; init; }
    public string ConnectionTechnology { get; init; } = string.Empty;
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DisplayOrientation Orientation { get; init; } = DisplayOrientation.Unknown;
    public uint RefreshRateNumerator { get; init; }
    public uint RefreshRateDenominator { get; init; }

    public double RefreshRateHz => RefreshRateDenominator == 0
        ? 0
        : (double)RefreshRateNumerator / RefreshRateDenominator;
}

public sealed class DisplayLayoutSnapshot
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<DisplayPathSnapshot> Displays { get; init; } = [];
}

public sealed class DisplayPathSnapshot
{
    public DisplayIdentity Identity { get; init; } = new();
    public bool IsActive { get; init; }
    public bool IsPrimary { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DisplayOrientation Orientation { get; init; } = DisplayOrientation.Unknown;
    public uint RefreshRateNumerator { get; init; }
    public uint RefreshRateDenominator { get; init; }
    public uint SourceId { get; init; }
    public uint TargetId { get; init; }
    public long AdapterLuid { get; init; }
    public string SourceDeviceName { get; init; } = string.Empty;
    public string ConnectionTechnology { get; init; } = string.Empty;

    public double RefreshRateHz => RefreshRateDenominator == 0
        ? 0
        : (double)RefreshRateNumerator / RefreshRateDenominator;
}

public sealed class DisplayOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
