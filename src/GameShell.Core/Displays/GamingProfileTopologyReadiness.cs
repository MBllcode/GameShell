using GameShell.Core.Settings;
namespace GameShell.Core.Displays;

public enum ProfileDisplayReadiness
{
    Disabled,
    Ready,
    ResolvableInactive,
    Missing,
    Degraded,
    Ambiguous,
    InvalidConfiguration
}

public enum ProfileDisplayReadinessReason
{
    None,
    PhysicalTargetMissing,
    ExpectedModeUnavailable,
    ModeInformationUnavailable,
    AmbiguousIdentity,
    InvalidExpectedMode,
    ModeInventoryInvalid,
    CapabilityBelowBestKnown,
    TargetInactive
}

public sealed class ProfileDisplayTargetReadiness
{
    public ProfileDisplayTarget SavedTarget { get; init; } = new();
    public DisplayDeviceInfo? ResolvedTarget { get; init; }
    public DisplayIdentityMatchConfidence PhysicalMatchConfidence { get; init; }
    public IReadOnlyList<DisplayModeInfo> AvailableModes { get; init; } = [];
    public DisplayCapabilitySnapshot? CurrentCapability { get; init; }
    public DisplayCapabilitySnapshot? BestKnownCapability { get; init; }
    public int AvailableModesForRequestedResolution { get; init; }
    public ProfileDisplayReadiness Result { get; init; }
    public ProfileDisplayReadinessReason Reason { get; init; }
}

public sealed class GamingProfileTopologyReadiness
{
    public string ProfileId { get; init; } = string.Empty;
    public string ProfileName { get; init; } = string.Empty;
    public IReadOnlyList<ProfileDisplayTargetReadiness> Targets { get; init; } = [];
    public bool IdentityCatalogChanged { get; init; }
    public bool IsReady => Targets.All(target => target.Result is ProfileDisplayReadiness.Ready or ProfileDisplayReadiness.ResolvableInactive or ProfileDisplayReadiness.Disabled);
    public string ProfileResult => IsReady ? "Ready" : "Blocked";
}
