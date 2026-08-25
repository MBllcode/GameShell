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
    HistoricalRouteAlias,
    ExactMonitorDevicePath,
    IdentityConflict
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

public enum DisplayCatalogState
{
    Current,
    Degraded,
    InactiveObservation,
    Unavailable,
    IdentityConflict
}

public enum DisplayDiscoveryState
{
    Idle,
    Loading,
    Settling,
    Ready,
    Error
}

public enum DisplayCapabilityState
{
    UnknownUntilActive,
    Available
}

public sealed class DisplayCapabilitySnapshot
{
    public int Width { get; init; }
    public int Height { get; init; }
    public uint MaxRefreshRateNumerator { get; init; }
    public uint MaxRefreshRateDenominator { get; init; }

    public double MaxRefreshRateHz => MaxRefreshRateDenominator == 0
        ? 0
        : (double)MaxRefreshRateNumerator / MaxRefreshRateDenominator;
}

public sealed class CurrentDisplayObservation
{
    public DisplayIdentity Identity { get; init; } = new();
    public string CurrentObservedName { get; init; } = string.Empty;
    public string CurrentSourceDeviceName { get; init; } = string.Empty;
    public string CurrentConnectionTechnology { get; init; } = string.Empty;

    // Kept for payload compatibility with clients built before the naming
    // distinction was introduced. New consumers should use CurrentObservedName.
    public string CurrentFriendlyName { get; init; } = string.Empty;
    public DisplayCapabilitySnapshot? CurrentCapability { get; init; }
    public IReadOnlyList<DisplayModeInfo> EnumeratedModes { get; init; } = [];
    public DisplayCapabilityState CapabilityState { get; init; } = DisplayCapabilityState.UnknownUntilActive;
    public bool IsActive { get; init; }
    public bool IsAvailable { get; init; }
    public DisplayCatalogState State { get; init; } = DisplayCatalogState.Current;
}

public sealed class KnownPhysicalDisplay
{
    public string Id { get; set; } = string.Empty;
    public DisplayIdentity PhysicalIdentity { get; set; } = new();
    public string CanonicalFriendlyName { get; set; } = string.Empty;
    public string OptionalUserLabel { get; set; } = string.Empty;
    public List<DisplayIdentity> HistoricalIdentities { get; set; } = [];
    public DisplayCapabilitySnapshot? BestKnownCapability { get; set; }
    public DisplayCapabilitySnapshot? LastVerifiedActiveCapability { get; set; }
    public string CapabilityVerificationSource { get; set; } = string.Empty;

    // Read-only compatibility with the pre-catalog profile/editor payload.
    // Validation migrates this value into BestKnownCapability and clears it
    // before the next persisted write.
    public DisplayCapabilitySnapshot? LastKnownGoodCapability { get; set; }
}

public sealed class DisplayDeviceInfo
{
    public string Id { get; init; } = string.Empty;
    public string KnownPhysicalDisplayId { get; init; } = string.Empty;
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
    public bool IsRemembered { get; init; }
    public DisplayCatalogState CatalogState { get; init; } = DisplayCatalogState.Current;
    public CurrentDisplayObservation? CurrentObservation { get; init; }
    public IReadOnlyList<DisplayModeInfo> EnumeratedModes { get; init; } = [];
    public DisplayCapabilitySnapshot? LastKnownGoodCapability { get; init; }
    public DisplayCapabilitySnapshot? LastVerifiedActiveCapability { get; init; }

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
    public string KnownPhysicalDisplayId { get; init; } = string.Empty;
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
