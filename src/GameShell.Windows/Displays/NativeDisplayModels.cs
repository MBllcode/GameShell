namespace GameShell.Windows.Displays;

internal sealed record NativeDisplayTopology(
    IReadOnlyList<NativeDisplayPath> Paths,
    string PrimarySourceDeviceName,
    DateTimeOffset CapturedAt);

internal sealed record NativeDisplayPath
{
    public string SourceDeviceName { get; init; } = string.Empty;
    public string MonitorFriendlyName { get; init; } = string.Empty;
    public string MonitorDevicePath { get; init; } = string.Empty;
    public uint EdidManufacturerId { get; init; }
    public uint EdidProductCodeId { get; init; }
    public uint ConnectorInstance { get; init; }
    public long AdapterLuid { get; init; }
    public uint SourceId { get; init; }
    public uint TargetId { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public NativeDisplayRotation Rotation { get; init; } = NativeDisplayRotation.Unknown;
    public uint RefreshRateNumerator { get; init; }
    public uint RefreshRateDenominator { get; init; }
    public string ConnectionTechnology { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public bool IsAvailable { get; init; } = true;
    public bool HasEdid { get; init; }
    public DisplayConfigPathInfo PathInfo { get; init; }
    public DisplayConfigSourceMode SourceMode { get; init; }
    public DisplayConfigTargetMode? TargetMode { get; init; }

    public double RefreshRateHz()
    {
        return RefreshRateDenominator == 0
            ? 0
            : (double)RefreshRateNumerator / RefreshRateDenominator;
    }
}

internal enum NativeDisplayRotation
{
    Identity,
    Rotate90,
    Rotate180,
    Rotate270,
    Unknown
}
