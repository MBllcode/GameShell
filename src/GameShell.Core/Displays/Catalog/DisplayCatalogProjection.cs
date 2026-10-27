
namespace GameShell.Core.Displays;

internal static class DisplayCatalogProjection
{
    internal static DisplayDeviceInfo CreateMergedDisplay(KnownPhysicalDisplay known, DisplayDeviceInfo current, bool identityConflict)
    {
        var identity = DisplayCatalogIdentity.CanonicalIdentity(known);
        var historicalCapability = DisplayCapabilityPolicy.GetBestKnown(known);
        var displayCapability = historicalCapability ?? DisplayCapabilityPolicy.CreateCapability(current);
        var state = ResolveState(current, DisplayCapabilityPolicy.GetVerifiedActive(known) ?? historicalCapability);
        var logicalState = identityConflict ? DisplayCatalogState.IdentityConflict : state;
        return new DisplayDeviceInfo
        {
            Id = DisplayCatalogIdentity.BuildIdentityKey(identity),
            KnownPhysicalDisplayId = known.Id,
            Name = DisplayCatalogIdentity.ResolveFriendlyName(known),
            IsPrimary = current.IsPrimary,
            IsEnabled = current.IsEnabled,
            IsAvailable = current.IsAvailable,
            Identity = identity,
            SourceDeviceName = current.SourceDeviceName,
            SourceId = current.SourceId,
            TargetId = current.TargetId,
            AdapterLuid = current.AdapterLuid,
            ConnectionTechnology = current.ConnectionTechnology,
            PositionX = current.PositionX,
            PositionY = current.PositionY,
            Width = displayCapability?.Width > 0 ? displayCapability.Width : current.Width,
            Height = displayCapability?.Height > 0 ? displayCapability.Height : current.Height,
            Orientation = current.Orientation,
            RefreshRateNumerator = current.RefreshRateNumerator,
            RefreshRateDenominator = current.RefreshRateDenominator,
            IsRemembered = true,
            CatalogState = logicalState,
            CurrentObservation = CreateObservation(current, logicalState),
            EnumeratedModes = current.EnumeratedModes,
            LastKnownGoodCapability = historicalCapability,
            LastVerifiedActiveCapability = DisplayCapabilityPolicy.GetVerifiedActive(known)
        };
    }

    internal static DisplayDeviceInfo CreateUnavailableDisplay(KnownPhysicalDisplay known)
    {
        var identity = DisplayCatalogIdentity.CanonicalIdentity(known);
        var capability = DisplayCapabilityPolicy.GetBestKnown(known);
        return new DisplayDeviceInfo
        {
            Id = DisplayCatalogIdentity.BuildIdentityKey(identity),
            KnownPhysicalDisplayId = known.Id,
            Name = DisplayCatalogIdentity.ResolveFriendlyName(known),
            IsEnabled = false,
            IsAvailable = false,
            Identity = identity,
            ConnectionTechnology = identity.OutputTechnology,
            Width = capability?.Width ?? 0,
            Height = capability?.Height ?? 0,
            RefreshRateNumerator = capability?.MaxRefreshRateNumerator ?? 0,
            RefreshRateDenominator = capability?.MaxRefreshRateDenominator ?? 0,
            IsRemembered = true,
            CatalogState = DisplayCatalogState.Unavailable,
            EnumeratedModes = [],
            LastKnownGoodCapability = capability,
            LastVerifiedActiveCapability = DisplayCapabilityPolicy.GetVerifiedActive(known)
        };
    }

    internal static CurrentDisplayObservation CreateObservation(DisplayDeviceInfo current, DisplayCatalogState state)
    {
        return new CurrentDisplayObservation
        {
            Identity = current.Identity,
            CurrentObservedName = DisplayCatalogIdentity.ResolveFriendlyName(current),
            CurrentSourceDeviceName = current.SourceDeviceName,
            CurrentConnectionTechnology = current.ConnectionTechnology,
            CurrentFriendlyName = DisplayCatalogIdentity.ResolveFriendlyName(current),
            CurrentCapability = DisplayCapabilityPolicy.CreateCapability(current),
            EnumeratedModes = current.EnumeratedModes,
            CapabilityState = current.IsEnabled
                ? DisplayCapabilityState.Available
                : DisplayCapabilityState.UnknownUntilActive,
            IsActive = current.IsEnabled,
            IsAvailable = current.IsAvailable,
            State = state
        };
    }

    internal static DisplayCatalogState ResolveState(DisplayDeviceInfo current, DisplayCapabilitySnapshot? bestKnown)
    {
        if (!current.IsAvailable)
        {
            return DisplayCatalogState.Unavailable;
        }

        if (!current.IsEnabled)
        {
            return DisplayCatalogState.InactiveObservation;
        }

        return DisplayCapabilityPolicy.IsCapabilityRegression(DisplayCapabilityPolicy.CreateCapability(current), bestKnown)
            ? DisplayCatalogState.Degraded
            : DisplayCatalogState.Current;
    }
}
