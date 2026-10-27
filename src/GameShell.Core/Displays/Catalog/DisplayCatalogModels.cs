
namespace GameShell.Core.Displays;

public sealed record DisplayCatalogMergeResult(
    IReadOnlyList<DisplayDeviceInfo> Displays,
    bool CatalogChanged,
    int KnownCount,
    int ObservationCount,
    int LogicalCount,
    int DuplicatesCollapsed,
    int MatchedCount,
    int NewCount,
    int ConflictCount,
    IReadOnlyList<DisplayIdentityMatchRecord> IdentityMatches)
{
    public int DeferredCount { get; init; }
}

public sealed record DisplayIdentityMatchRecord(
    string KnownDisplayId,
    string CanonicalName,
    string ObservedName,
    long AdapterLuid,
    uint TargetId,
    DisplayIdentityMatchConfidence MatchConfidence,
    string Result)
{
    public long SavedAdapterLuid { get; init; }
    public uint SavedTargetId { get; init; }
    public bool MonitorDevicePathMatch { get; init; }
    public bool EdidMatch { get; init; }
    public bool HistoricalRouteMatch { get; init; }
    public bool MissingMetadata { get; init; }
    public bool ConflictingMetadata { get; init; }
}

internal sealed record KnownDisplayMatch(
    KnownPhysicalDisplay Known,
    DisplayIdentityMatchResult Match,
    int Score);

public sealed record CapabilityCatalogUpdate(
    DisplayCapabilitySnapshot? PreviousBest,
    DisplayCapabilitySnapshot? NewBest,
    string Action);
