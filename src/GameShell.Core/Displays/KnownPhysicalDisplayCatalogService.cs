using GameShell.Core.Settings;

namespace GameShell.Core.Displays;

public static class KnownPhysicalDisplayCatalogService
{
    public static bool HasKnownDisplayReconciliationCandidates(DisplaySettings settings) =>
        DisplayCatalogReconciliation.HasKnownDisplayReconciliationCandidates(settings);

    public static bool NormalizeCanonicalNames(DisplaySettings settings) =>
        DisplayCatalogReconciliation.NormalizeCanonicalNames(settings);

    public static bool RecordIdentityObservation(DisplaySettings settings, DisplayIdentity observedIdentity) =>
        DisplayCatalogIdentity.RecordIdentityObservation(settings, observedIdentity);

    public static KnownPhysicalDisplay? FindKnownByIdentity(DisplaySettings settings, DisplayIdentity identity) =>
        DisplayCatalogIdentity.FindKnownByIdentity(settings, identity);

    public static KnownPhysicalDisplay? FindKnownById(DisplaySettings settings, string? knownPhysicalDisplayId) =>
        DisplayCatalogIdentity.FindKnownById(settings, knownPhysicalDisplayId);

    public static string ResolveCanonicalName(DisplaySettings settings, DisplayIdentity identity) =>
        DisplayCatalogIdentity.ResolveCanonicalName(settings, identity);

    public static DisplayCapabilitySnapshot? FindBestKnown(DisplaySettings settings, DisplayIdentity identity) =>
        DisplayCatalogIdentity.FindBestKnown(settings, identity);

    public static DisplayCapabilitySnapshot? GetBestKnownCapability(KnownPhysicalDisplay? known)
    {
        return known is null ? null : DisplayCapabilityPolicy.GetBestKnown(known);
    }

    public static bool MigrateLegacyProfileCapabilities(DisplaySettings settings)
    {
        settings.KnownPhysicalDisplays ??= [];
        var changed = DisplayCatalogReconciliation.NormalizeKnownDisplays(settings);

        foreach (var profile in settings.GamingProfiles ?? [])
        {
            foreach (var target in profile.DisplayTopology?.Displays ?? [])
            {
                var legacy = target.CapabilityExpectation;
                if (!DisplayCatalogIdentity.HasPhysicalIdentity(target.Identity))
                {
                    continue;
                }

                var known = DisplayCatalogIdentity.FindKnown(settings, target.Identity);
                if (known is null)
                {
                    known = new KnownPhysicalDisplay
                    {
                        Id = DisplayCatalogIdentity.NewKnownDisplayId(),
                        PhysicalIdentity = target.Identity,
                        CanonicalFriendlyName = target.Identity.FriendlyName,
                        BestKnownCapability = DisplayCapabilityPolicy.IsValidCapability(legacy) ? DisplayCapabilityPolicy.ToSnapshot(legacy!) : null,
                        LastVerifiedActiveCapability = DisplayCapabilityPolicy.IsValidCapability(legacy) ? DisplayCapabilityPolicy.ToSnapshot(legacy!) : null,
                        CapabilityVerificationSource = DisplayCapabilityPolicy.IsValidCapability(legacy) ? "LegacyProfileCapability" : string.Empty
                    };
                    settings.KnownPhysicalDisplays.Add(known);
                    changed = true;
                }
                else
                {
                    changed |= DisplayCatalogIdentity.EnsureCanonicalName(known, target.Identity.FriendlyName);
                    var enriched = DisplayCatalogIdentity.EnrichIdentity(known.PhysicalIdentity, target.Identity, DisplayCatalogIdentity.ResolveFriendlyName(known));
                    if (!Equals(enriched, known.PhysicalIdentity))
                    {
                        known.PhysicalIdentity = enriched;
                        changed = true;
                    }

                    if (DisplayCapabilityPolicy.IsValidCapability(legacy)
                        && DisplayCapabilityPolicy.IsBetterCapability(DisplayCapabilityPolicy.ToSnapshot(legacy!), DisplayCapabilityPolicy.GetBestKnown(known)))
                    {
                        known.BestKnownCapability = DisplayCapabilityPolicy.ToSnapshot(legacy!);
                        known.LastVerifiedActiveCapability = DisplayCapabilityPolicy.ToSnapshot(legacy!);
                        known.CapabilityVerificationSource = "LegacyProfileCapability";
                        changed = true;
                    }
                }

                if (!string.Equals(target.KnownPhysicalDisplayId, known.Id, StringComparison.Ordinal))
                {
                    target.KnownPhysicalDisplayId = known.Id;
                    changed = true;
                }

                if (legacy is not null)
                {
                    target.CapabilityExpectation = null;
                    changed = true;
                }
            }
        }

        return changed;
    }

    public static bool RecordHealthyObservation(
        DisplaySettings settings,
        DisplayIdentity physicalIdentity,
        DisplayCapabilitySnapshot observed,
        out CapabilityCatalogUpdate update)
    {
        settings.KnownPhysicalDisplays ??= [];
        var changed = DisplayCatalogReconciliation.NormalizeKnownDisplays(settings);
        var known = DisplayCatalogIdentity.FindKnown(settings, physicalIdentity);
        if (known is null)
        {
            known = new KnownPhysicalDisplay
            {
                Id = DisplayCatalogIdentity.NewKnownDisplayId(),
                PhysicalIdentity = physicalIdentity,
                CanonicalFriendlyName = physicalIdentity.FriendlyName,
                BestKnownCapability = observed,
                LastVerifiedActiveCapability = observed,
                CapabilityVerificationSource = "ActiveRuntime"
            };
            settings.KnownPhysicalDisplays.Add(known);
            update = new CapabilityCatalogUpdate(null, observed, "Initialized");
            return true;
        }

        var previous = DisplayCapabilityPolicy.GetBestKnown(known);
        var previousVerified = DisplayCapabilityPolicy.GetVerifiedActive(known);
        changed |= DisplayCatalogIdentity.RecordIdentityObservation(known, physicalIdentity);
        update = new CapabilityCatalogUpdate(previous, previous, previous is null ? "Initialized" : "Kept");
        if (DisplayCapabilityPolicy.IsBetterCapability(observed, previous))
        {
            known.BestKnownCapability = observed;
            update = new CapabilityCatalogUpdate(previous, observed, previous is null ? "Initialized" : "Upgraded");
            changed = true;
        }

        if (DisplayCapabilityPolicy.IsBetterCapability(observed, previousVerified))
        {
            known.LastVerifiedActiveCapability = observed;
            known.CapabilityVerificationSource = "ActiveRuntime";
            changed = true;
        }

        return changed;
    }

    public static DisplayCatalogMergeResult Merge(
        DisplaySettings settings,
        IReadOnlyList<DisplayDeviceInfo> currentDisplays)
    {
        return Merge(settings, currentDisplays, deferTransientCandidates: false);
    }

    public static DisplayCatalogMergeResult Merge(
        DisplaySettings settings,
        IReadOnlyList<DisplayDeviceInfo> currentDisplays,
        bool deferTransientCandidates)
    {
        settings.KnownPhysicalDisplays ??= [];
        var changed = DisplayCatalogReconciliation.NormalizeKnownDisplays(settings);
        var knownDisplays = settings.KnownPhysicalDisplays;
        var currentMatches = new Dictionary<KnownPhysicalDisplay, DisplayDeviceInfo>();
        var conflictedDisplays = new HashSet<KnownPhysicalDisplay>();
        var matchedCount = 0;
        var newCount = 0;
        var conflictCount = 0;
        var duplicatesCollapsed = 0;
        var identityMatches = new List<DisplayIdentityMatchRecord>();

        foreach (var current in currentDisplays)
        {
            var knownMatch = DisplayCatalogIdentity.FindKnownMatch(settings, current.Identity);
            var known = knownMatch?.Known;
            if (known is null)
            {
                var conflict = DisplayCatalogIdentity.FindIdentityConflict(settings, current.Identity);
                if (deferTransientCandidates
                    && conflict is null
                    && IsTransientReconnectCandidate(settings, current.Identity))
                {
                    identityMatches.Add(new DisplayIdentityMatchRecord(
                        string.Empty,
                        DisplayCatalogIdentity.ResolveCandidateCanonicalName(settings, current.Identity),
                        DisplayCatalogIdentity.ResolveFriendlyName(current),
                        current.Identity.AdapterLuid,
                        current.Identity.TargetId,
                        DisplayIdentityMatchConfidence.None,
                        "Deferred")
                    {
                        MissingMetadata = true
                    });
                    continue;
                }

                known = new KnownPhysicalDisplay
                {
                    Id = DisplayCatalogIdentity.NewKnownDisplayId(),
                    PhysicalIdentity = current.Identity,
                    CanonicalFriendlyName = DisplayCatalogIdentity.ResolveFriendlyName(current)
                };
                knownDisplays.Add(known);
                changed = true;
                newCount++;
                if (conflict is not null)
                {
                    conflictedDisplays.Add(known);
                    conflictCount++;
                    identityMatches.Add(new DisplayIdentityMatchRecord(
                        conflict.Id,
                        DisplayCatalogIdentity.ResolveCanonicalFriendlyName(conflict),
                        DisplayCatalogIdentity.ResolveFriendlyName(current),
                        current.Identity.AdapterLuid,
                        current.Identity.TargetId,
                        DisplayIdentityMatchConfidence.IdentityConflict,
                        "Conflict"));
                }
                else
                {
                    identityMatches.Add(new DisplayIdentityMatchRecord(
                        known.Id,
                        DisplayCatalogIdentity.ResolveCanonicalFriendlyName(known),
                        DisplayCatalogIdentity.ResolveFriendlyName(current),
                        current.Identity.AdapterLuid,
                        current.Identity.TargetId,
                        DisplayIdentityMatchConfidence.None,
                        "New"));
                }
            }
            else
            {
                matchedCount++;
                identityMatches.Add(new DisplayIdentityMatchRecord(
                    known.Id,
                    DisplayCatalogIdentity.ResolveCanonicalFriendlyName(known),
                    DisplayCatalogIdentity.ResolveFriendlyName(current),
                    current.Identity.AdapterLuid,
                    current.Identity.TargetId,
                    knownMatch!.Match.MatchConfidence,
                    "MatchedExisting")
                {
                    SavedAdapterLuid = known.PhysicalIdentity.AdapterLuid,
                    SavedTargetId = known.PhysicalIdentity.TargetId,
                    MonitorDevicePathMatch = knownMatch.Match.MonitorDevicePathMatch,
                    EdidMatch = knownMatch.Match.EdidMatch,
                    HistoricalRouteMatch = knownMatch.Match.HistoricalRouteMatch,
                    MissingMetadata = knownMatch.Match.MissingMetadata,
                    ConflictingMetadata = knownMatch.Match.HasConflict
                });
                changed |= DisplayCatalogIdentity.RecordIdentityObservation(known, current.Identity);
                var enrichedIdentity = DisplayCatalogIdentity.EnrichIdentity(known.PhysicalIdentity, current.Identity, DisplayCatalogIdentity.ResolveFriendlyName(known));
                if (!Equals(enrichedIdentity, known.PhysicalIdentity))
                {
                    known.PhysicalIdentity = enrichedIdentity;
                    changed = true;
                }
            }

            if (currentMatches.TryGetValue(known, out var previous))
            {
                duplicatesCollapsed++;
            }

            if (previous is null
                || DisplayCapabilityPolicy.IsBetterCapability(DisplayCapabilityPolicy.CreateCapability(current), DisplayCapabilityPolicy.CreateCapability(previous)))
            {
                currentMatches[known] = current;
            }
        }

        var merged = knownDisplays
            .Select(known => currentMatches.TryGetValue(known, out var current)
                ? DisplayCatalogProjection.CreateMergedDisplay(known, current, conflictedDisplays.Contains(known))
                : DisplayCatalogProjection.CreateUnavailableDisplay(known))
            .ToList();

        return new DisplayCatalogMergeResult(
            merged,
            changed,
            knownDisplays.Count,
            currentDisplays.Count,
            merged.Count,
            duplicatesCollapsed,
            matchedCount,
            newCount,
            conflictCount,
            identityMatches)
        {
            DeferredCount = identityMatches.Count(match => string.Equals(match.Result, "Deferred", StringComparison.Ordinal))
        };
    }

    /// <summary>
    /// A reconnect candidate is intentionally only deferred here. It is never
    /// merged by friendly name or by this degraded route evidence.
    /// </summary>

    public static bool IsTransientReconnectCandidate(DisplaySettings settings, DisplayIdentity identity)
    {
        if (DisplayCatalogIdentity.HasStrongIdentity(identity))
        {
            return false;
        }

        // Route/name evidence is used only as a bounded deferral hint when
        // Windows has not emitted usable physical metadata yet. It never
        // authorizes a merge or a persisted identity.
        return settings.KnownPhysicalDisplays.Any(known =>
            (identity.TargetId != 0
                && known.PhysicalIdentity.TargetId == identity.TargetId
                && (known.PhysicalIdentity.AdapterLuid == 0
                    || identity.AdapterLuid == 0
                    || known.PhysicalIdentity.AdapterLuid != identity.AdapterLuid))
            || (identity.TargetId == 0
                && !string.IsNullOrWhiteSpace(identity.FriendlyName)
                && (string.Equals(known.CanonicalFriendlyName, identity.FriendlyName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(known.PhysicalIdentity.FriendlyName, identity.FriendlyName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(known.OptionalUserLabel, identity.FriendlyName, StringComparison.OrdinalIgnoreCase))));
    }
}
