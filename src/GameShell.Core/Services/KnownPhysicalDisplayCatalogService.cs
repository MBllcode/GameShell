using GameShell.Core.Models;

namespace GameShell.Core.Services;

/// <summary>
/// Owns the persisted capability knowledge for physical displays. Current
/// discovery is only an observation; it may initialize an unknown target, but
/// it cannot lower an existing best-known capability.
/// </summary>
public static class KnownPhysicalDisplayCatalogService
{
    public static DisplayCapabilitySnapshot? FindBestKnown(
        DisplaySettings settings,
        DisplayIdentity identity)
    {
        var known = settings.KnownPhysicalDisplays?
            .Select(candidate => new
            {
                Candidate = candidate,
                Match = DisplayIdentityMatcher.ResolveKnownDisplay(candidate, identity),
                Score = DisplayIdentityMatcher.GetStableIdentityScore(candidate, identity)
            })
            .Where(candidate => candidate.Match.IsMatch)
            .OrderByDescending(candidate => candidate.Match.MatchConfidence)
            .ThenByDescending(candidate => candidate.Score)
            .Select(candidate => candidate.Candidate)
            .FirstOrDefault();
        return known is null ? null : GetBestKnown(known);
    }

    public static string ResolveCanonicalName(DisplaySettings settings, DisplayIdentity identity)
    {
        var known = FindKnown(settings, identity);
        return known is null ? identity.FriendlyName : ResolveFriendlyName(known);
    }

    public static KnownPhysicalDisplay? FindKnownById(DisplaySettings settings, string? knownPhysicalDisplayId)
    {
        return string.IsNullOrWhiteSpace(knownPhysicalDisplayId)
            ? null
            : settings.KnownPhysicalDisplays?.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, knownPhysicalDisplayId, StringComparison.OrdinalIgnoreCase));
    }

    public static KnownPhysicalDisplay? FindKnownByIdentity(DisplaySettings settings, DisplayIdentity identity)
    {
        return FindKnown(settings, identity);
    }

    public static bool MigrateLegacyProfileCapabilities(DisplaySettings settings)
    {
        settings.KnownPhysicalDisplays ??= [];
        var changed = NormalizeKnownDisplays(settings);

        foreach (var profile in settings.GamingProfiles ?? [])
        {
            foreach (var target in profile.DisplayTopology?.Displays ?? [])
            {
                var legacy = target.CapabilityExpectation;
                if (!HasPhysicalIdentity(target.Identity))
                {
                    continue;
                }

                var known = FindKnown(settings, target.Identity);
                if (known is null)
                {
                    known = new KnownPhysicalDisplay
                    {
                        Id = NewKnownDisplayId(),
                        PhysicalIdentity = target.Identity,
                        CanonicalFriendlyName = target.Identity.FriendlyName,
                        BestKnownCapability = IsValidCapability(legacy) ? ToSnapshot(legacy!) : null,
                        LastVerifiedActiveCapability = IsValidCapability(legacy) ? ToSnapshot(legacy!) : null,
                        CapabilityVerificationSource = IsValidCapability(legacy) ? "LegacyProfileCapability" : string.Empty
                    };
                    settings.KnownPhysicalDisplays.Add(known);
                    changed = true;
                }
                else
                {
                    changed |= EnsureCanonicalName(known, target.Identity.FriendlyName);
                    var enriched = EnrichIdentity(known.PhysicalIdentity, target.Identity, ResolveFriendlyName(known));
                    if (!Equals(enriched, known.PhysicalIdentity))
                    {
                        known.PhysicalIdentity = enriched;
                        changed = true;
                    }

                    if (IsValidCapability(legacy)
                        && IsBetterCapability(ToSnapshot(legacy!), GetBestKnown(known)))
                    {
                        known.BestKnownCapability = ToSnapshot(legacy!);
                        known.LastVerifiedActiveCapability = ToSnapshot(legacy!);
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

    public static bool NormalizeCanonicalNames(DisplaySettings settings)
    {
        settings.ConsoleDisplays ??= [];
        var changed = false;

        foreach (var profile in settings.GamingProfiles ?? [])
        {
            foreach (var target in profile.DisplayTopology?.Displays ?? [])
            {
                changed |= NormalizeTargetReference(settings, target);
            }

            profile.Layout = NormalizeLayoutNames(settings, profile.Layout, ref changed);
        }

        if (settings.GamingLayout is not null)
        {
            settings.GamingLayout = NormalizeLayoutNames(settings, settings.GamingLayout, ref changed);
        }

        if (settings.ConsoleDisplay is not null)
        {
            changed |= NormalizeIdentityName(settings, settings.ConsoleDisplay, identity => settings.ConsoleDisplay = identity);
        }

        for (var index = 0; index < settings.ConsoleDisplays.Count; index++)
        {
            var current = settings.ConsoleDisplays[index];
            changed |= NormalizeIdentityName(settings, current, identity => settings.ConsoleDisplays[index] = identity);
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
        var changed = NormalizeKnownDisplays(settings);
        var known = FindKnown(settings, physicalIdentity);
        if (known is null)
        {
            known = new KnownPhysicalDisplay
            {
                Id = NewKnownDisplayId(),
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

        var previous = GetBestKnown(known);
        var previousVerified = GetVerifiedActive(known);
        changed |= RecordIdentityObservation(known, physicalIdentity);
        update = new CapabilityCatalogUpdate(previous, previous, previous is null ? "Initialized" : "Kept");
        if (IsBetterCapability(observed, previous))
        {
            known.BestKnownCapability = observed;
            update = new CapabilityCatalogUpdate(previous, observed, previous is null ? "Initialized" : "Upgraded");
            changed = true;
        }

        if (IsBetterCapability(observed, previousVerified))
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
        settings.KnownPhysicalDisplays ??= [];
        var changed = NormalizeKnownDisplays(settings);
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
            var knownMatch = FindKnownMatch(settings, current.Identity);
            var known = knownMatch?.Known;
            if (known is null)
            {
                var conflict = FindIdentityConflict(settings, current.Identity);
                known = new KnownPhysicalDisplay
                {
                    Id = NewKnownDisplayId(),
                    PhysicalIdentity = current.Identity,
                    CanonicalFriendlyName = ResolveFriendlyName(current)
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
                        ResolveCanonicalFriendlyName(conflict),
                        ResolveFriendlyName(current),
                        current.Identity.AdapterLuid,
                        current.Identity.TargetId,
                        DisplayIdentityMatchConfidence.IdentityConflict,
                        "Conflict"));
                }
                else
                {
                    identityMatches.Add(new DisplayIdentityMatchRecord(
                        known.Id,
                        ResolveCanonicalFriendlyName(known),
                        ResolveFriendlyName(current),
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
                    ResolveCanonicalFriendlyName(known),
                    ResolveFriendlyName(current),
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
                changed |= RecordIdentityObservation(known, current.Identity);
                var enrichedIdentity = EnrichIdentity(known.PhysicalIdentity, current.Identity, ResolveFriendlyName(known));
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
                || IsBetterCapability(CreateCapability(current), CreateCapability(previous)))
            {
                currentMatches[known] = current;
            }
        }

        var merged = knownDisplays
            .Select(known => currentMatches.TryGetValue(known, out var current)
                ? CreateMergedDisplay(known, current, conflictedDisplays.Contains(known))
                : CreateUnavailableDisplay(known))
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
            identityMatches);
    }

    private static KnownPhysicalDisplay? FindKnown(DisplaySettings settings, DisplayIdentity identity)
    {
        return FindKnownMatch(settings, identity)?.Known;
    }

    private static KnownDisplayMatch? FindKnownMatch(DisplaySettings settings, DisplayIdentity identity)
    {
        return settings.KnownPhysicalDisplays
            .Select(candidate => new KnownDisplayMatch(
                candidate,
                DisplayIdentityMatcher.ResolveKnownDisplay(candidate, identity),
                DisplayIdentityMatcher.GetStableIdentityScore(candidate, identity)))
            .Where(candidate => candidate.Match.IsMatch)
            .OrderByDescending(candidate => candidate.Match.MatchConfidence)
            .ThenByDescending(candidate => candidate.Score)
            .FirstOrDefault();
    }

    private static KnownPhysicalDisplay? FindIdentityConflict(DisplaySettings settings, DisplayIdentity identity)
    {
        return settings.KnownPhysicalDisplays.FirstOrDefault(candidate =>
            DisplayIdentityMatcher.ResolveKnownDisplay(candidate, identity).HasConflict);
    }

    private static bool NormalizeKnownDisplays(DisplaySettings settings)
    {
        var changed = false;
        var knownDisplays = settings.KnownPhysicalDisplays;
        var result = new List<KnownPhysicalDisplay>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in knownDisplays)
        {
            if (candidate is null || !HasPhysicalIdentity(candidate.PhysicalIdentity))
            {
                changed = true;
                continue;
            }

            candidate.HistoricalIdentities ??= [];
            changed |= PromoteRicherPhysicalIdentity(candidate);

            if (string.IsNullOrWhiteSpace(candidate.Id) || !usedIds.Add(candidate.Id))
            {
                candidate.Id = Guid.NewGuid().ToString("N");
                usedIds.Add(candidate.Id);
                changed = true;
            }

            if (candidate.BestKnownCapability is null && candidate.LastKnownGoodCapability is not null)
            {
                candidate.BestKnownCapability = candidate.LastKnownGoodCapability;
                candidate.LastKnownGoodCapability = null;
                changed = true;
            }

            if (candidate.LastVerifiedActiveCapability is null && candidate.BestKnownCapability is not null)
            {
                candidate.LastVerifiedActiveCapability = candidate.BestKnownCapability;
                candidate.CapabilityVerificationSource = string.IsNullOrWhiteSpace(candidate.CapabilityVerificationSource)
                    ? "LegacyCatalogCapability"
                    : candidate.CapabilityVerificationSource;
                changed = true;
            }

            changed |= EnsureCanonicalName(candidate, candidate.PhysicalIdentity.FriendlyName);

            var existingIndex = result.FindIndex(item =>
                DisplayIdentityMatcher.CanReconcileKnownDisplays(item, candidate));
            if (existingIndex < 0)
            {
                result.Add(candidate);
                continue;
            }

            var existing = result[existingIndex];
            var survivor = ChooseSurvivor(settings, existing, candidate);
            var removed = ReferenceEquals(survivor, existing) ? candidate : existing;
            MergeKnownData(survivor, removed);
            changed |= RepointReferences(settings, removed.Id, survivor);
            if (ReferenceEquals(survivor, candidate))
            {
                result[existingIndex] = candidate;
            }

            changed = true;
        }

        if (changed)
        {
            knownDisplays.Clear();
            knownDisplays.AddRange(result);
        }

        return changed;
    }

    public static bool HasKnownDisplayReconciliationCandidates(DisplaySettings settings)
    {
        var knownDisplays = settings.KnownPhysicalDisplays ?? [];
        for (var left = 0; left < knownDisplays.Count; left++)
        {
            for (var right = left + 1; right < knownDisplays.Count; right++)
            {
                if (DisplayIdentityMatcher.CanReconcileKnownDisplays(knownDisplays[left], knownDisplays[right]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static KnownPhysicalDisplay ChooseSurvivor(
        DisplaySettings settings,
        KnownPhysicalDisplay existing,
        KnownPhysicalDisplay candidate)
    {
        var existingReferenced = IsKnownDisplayReferenced(settings, existing.Id);
        var candidateReferenced = IsKnownDisplayReferenced(settings, candidate.Id);
        if (existingReferenced != candidateReferenced)
        {
            return existingReferenced ? existing : candidate;
        }

        var existingRichness = GetKnownDisplayRichness(existing);
        var candidateRichness = GetKnownDisplayRichness(candidate);
        if (existingRichness != candidateRichness)
        {
            return existingRichness > candidateRichness ? existing : candidate;
        }

        // The catalog list is persisted in creation order. When all stronger
        // signals are equal, keep the earlier record so canonical naming and
        // existing references remain stable. Reconciliation already runs in a
        // deterministic order, so choosing the existing item is deterministic.
        return existing;
    }

    private static int GetKnownDisplayRichness(KnownPhysicalDisplay known)
    {
        var score = (known.PhysicalIdentity.AdapterLuid != 0 ? 1 : 0)
            + (known.PhysicalIdentity.TargetId != 0 ? 1 : 0)
            + (!string.IsNullOrWhiteSpace(known.PhysicalIdentity.MonitorDevicePath) ? 2 : 0)
            + (HasValidEdid(known.PhysicalIdentity) ? 2 : 0)
            + (known.PhysicalIdentity.ConnectorInstance != 0 ? 1 : 0)
            + (known.HistoricalIdentities?.Count ?? 0) * 2;
        if (GetBestKnown(known) is not null)
        {
            score += 3;
        }

        if (GetVerifiedActive(known) is not null)
        {
            score += 2;
        }

        return score;
    }

    private static bool IsKnownDisplayReferenced(DisplaySettings settings, string knownId)
    {
        if (string.IsNullOrWhiteSpace(knownId))
        {
            return false;
        }

        return (settings.GamingProfiles ?? []).Any(profile =>
            (profile.DisplayTopology?.Displays ?? []).Any(target => string.Equals(target.KnownPhysicalDisplayId, knownId, StringComparison.OrdinalIgnoreCase))
            || (profile.Layout?.Displays ?? []).Any(display => string.Equals(display.KnownPhysicalDisplayId, knownId, StringComparison.OrdinalIgnoreCase)))
            || (settings.GamingLayout?.Displays ?? []).Any(display => string.Equals(display.KnownPhysicalDisplayId, knownId, StringComparison.OrdinalIgnoreCase));
    }

    private static DisplayDeviceInfo CreateMergedDisplay(KnownPhysicalDisplay known, DisplayDeviceInfo current, bool identityConflict)
    {
        var identity = CanonicalIdentity(known);
        var historicalCapability = GetBestKnown(known);
        var displayCapability = historicalCapability ?? CreateCapability(current);
        var state = ResolveState(current, GetVerifiedActive(known) ?? historicalCapability);
        var logicalState = identityConflict ? DisplayCatalogState.IdentityConflict : state;
        return new DisplayDeviceInfo
        {
            Id = BuildIdentityKey(identity),
            KnownPhysicalDisplayId = known.Id,
            Name = ResolveFriendlyName(known),
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
            LastVerifiedActiveCapability = GetVerifiedActive(known)
        };
    }

    private static DisplayDeviceInfo CreateUnavailableDisplay(KnownPhysicalDisplay known)
    {
        var identity = CanonicalIdentity(known);
        var capability = GetBestKnown(known);
        return new DisplayDeviceInfo
        {
            Id = BuildIdentityKey(identity),
            KnownPhysicalDisplayId = known.Id,
            Name = ResolveFriendlyName(known),
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
            LastVerifiedActiveCapability = GetVerifiedActive(known)
        };
    }

    private static CurrentDisplayObservation CreateObservation(DisplayDeviceInfo current, DisplayCatalogState state)
    {
        return new CurrentDisplayObservation
        {
            Identity = current.Identity,
            CurrentObservedName = ResolveFriendlyName(current),
            CurrentSourceDeviceName = current.SourceDeviceName,
            CurrentConnectionTechnology = current.ConnectionTechnology,
            CurrentFriendlyName = ResolveFriendlyName(current),
            CurrentCapability = CreateCapability(current),
            EnumeratedModes = current.EnumeratedModes,
            CapabilityState = current.IsEnabled
                ? DisplayCapabilityState.Available
                : DisplayCapabilityState.UnknownUntilActive,
            IsActive = current.IsEnabled,
            IsAvailable = current.IsAvailable,
            State = state
        };
    }

    private static DisplayCatalogState ResolveState(DisplayDeviceInfo current, DisplayCapabilitySnapshot? bestKnown)
    {
        if (!current.IsAvailable)
        {
            return DisplayCatalogState.Unavailable;
        }

        if (!current.IsEnabled)
        {
            return DisplayCatalogState.InactiveObservation;
        }

        return IsCapabilityRegression(CreateCapability(current), bestKnown)
            ? DisplayCatalogState.Degraded
            : DisplayCatalogState.Current;
    }

    private static bool IsCapabilityRegression(DisplayCapabilitySnapshot? current, DisplayCapabilitySnapshot? known)
    {
        if (current is null || known is null || current.Width <= 0 || current.Height <= 0 || known.Width <= 0 || known.Height <= 0)
        {
            return false;
        }

        return current.Width == known.Width
            && current.Height == known.Height
            && current.MaxRefreshRateHz > 0
            && known.MaxRefreshRateHz > current.MaxRefreshRateHz + 0.25;
    }

    private static DisplayCapabilitySnapshot? CreateCapability(DisplayDeviceInfo display)
    {
        return display.Width > 0 && display.Height > 0 && display.RefreshRateNumerator > 0 && display.RefreshRateDenominator > 0
            ? new DisplayCapabilitySnapshot
            {
                Width = display.Width,
                Height = display.Height,
                MaxRefreshRateNumerator = display.RefreshRateNumerator,
                MaxRefreshRateDenominator = display.RefreshRateDenominator
            }
            : null;
    }

    private static bool IsBetterCapability(DisplayCapabilitySnapshot? candidate, DisplayCapabilitySnapshot? existing)
    {
        if (candidate is null)
        {
            return false;
        }

        if (existing is null)
        {
            return true;
        }

        var candidateArea = (long)candidate.Width * candidate.Height;
        var existingArea = (long)existing.Width * existing.Height;
        return candidateArea > existingArea
            || candidateArea == existingArea && candidate.MaxRefreshRateHz > existing.MaxRefreshRateHz + 0.25;
    }

    private static bool IsValidCapability(DisplayCapabilityExpectation? capability)
    {
        return capability is not null
            && capability.Width > 0
            && capability.Height > 0
            && capability.LastKnownGoodMaxRefreshNumerator > 0
            && capability.LastKnownGoodMaxRefreshDenominator > 0
            && double.IsFinite(capability.MaxRefreshRateHz);
    }

    private static DisplayCapabilitySnapshot ToSnapshot(DisplayCapabilityExpectation capability)
    {
        return new DisplayCapabilitySnapshot
        {
            Width = capability.Width,
            Height = capability.Height,
            MaxRefreshRateNumerator = capability.LastKnownGoodMaxRefreshNumerator,
            MaxRefreshRateDenominator = capability.LastKnownGoodMaxRefreshDenominator
        };
    }

    private static DisplayCapabilitySnapshot? GetBestKnown(KnownPhysicalDisplay known)
    {
        return known.BestKnownCapability ?? known.LastKnownGoodCapability;
    }

    private static DisplayCapabilitySnapshot? GetVerifiedActive(KnownPhysicalDisplay known)
    {
        return known.LastVerifiedActiveCapability ?? GetBestKnown(known);
    }

    private static DisplayIdentity CanonicalIdentity(KnownPhysicalDisplay known)
    {
        return known.PhysicalIdentity with { FriendlyName = ResolveCanonicalFriendlyName(known) };
    }

    private static DisplayIdentity EnrichIdentity(DisplayIdentity existing, DisplayIdentity candidate, string canonicalName)
    {
        return existing with
        {
            MonitorDevicePath = Prefer(existing.MonitorDevicePath, candidate.MonitorDevicePath),
            FriendlyName = canonicalName,
            EdidIdsValid = existing.EdidIdsValid || candidate.EdidIdsValid,
            EdidManufacturerId = existing.EdidManufacturerId != 0 ? existing.EdidManufacturerId : candidate.EdidManufacturerId,
            EdidProductCodeId = existing.EdidProductCodeId != 0 ? existing.EdidProductCodeId : candidate.EdidProductCodeId,
            ConnectorInstance = existing.ConnectorInstance != 0 ? existing.ConnectorInstance : candidate.ConnectorInstance,
            OutputTechnology = Prefer(existing.OutputTechnology, candidate.OutputTechnology),
            AdapterLuid = existing.AdapterLuid != 0 ? existing.AdapterLuid : candidate.AdapterLuid,
            TargetId = existing.TargetId != 0 ? existing.TargetId : candidate.TargetId
        };
    }

    public static bool RecordIdentityObservation(
        DisplaySettings settings,
        DisplayIdentity observedIdentity)
    {
        var known = FindKnown(settings, observedIdentity);
        return known is not null && RecordIdentityObservation(known, observedIdentity);
    }

    private static bool RecordIdentityObservation(
        KnownPhysicalDisplay known,
        DisplayIdentity observedIdentity)
    {
        var match = DisplayIdentityMatcher.ResolveKnownDisplay(known, observedIdentity);
        if (!match.IsMatch)
        {
            return false;
        }

        known.HistoricalIdentities ??= [];
        if (known.PhysicalIdentity.Equals(observedIdentity)
            || known.HistoricalIdentities.Any(identity => identity.Equals(observedIdentity)))
        {
            return false;
        }

        known.HistoricalIdentities.Add(observedIdentity);
        return true;
    }

    private static void MergeKnownData(
        KnownPhysicalDisplay survivor,
        KnownPhysicalDisplay removed)
    {
        survivor.HistoricalIdentities ??= [];
        AddHistoricalIdentity(survivor, removed.PhysicalIdentity);
        foreach (var identity in removed.HistoricalIdentities ?? [])
        {
            AddHistoricalIdentity(survivor, identity);
        }

        survivor.PhysicalIdentity = EnrichIdentity(
            survivor.PhysicalIdentity,
            removed.PhysicalIdentity,
            ResolveFriendlyName(survivor));
        if (string.IsNullOrWhiteSpace(survivor.CanonicalFriendlyName))
        {
            survivor.CanonicalFriendlyName = removed.CanonicalFriendlyName;
        }

        if (string.IsNullOrWhiteSpace(survivor.OptionalUserLabel))
        {
            survivor.OptionalUserLabel = removed.OptionalUserLabel;
        }

        if (IsBetterCapability(GetBestKnown(removed), GetBestKnown(survivor)))
        {
            survivor.BestKnownCapability = GetBestKnown(removed);
        }

        if (IsBetterCapability(GetVerifiedActive(removed), GetVerifiedActive(survivor)))
        {
            survivor.LastVerifiedActiveCapability = GetVerifiedActive(removed);
            survivor.CapabilityVerificationSource = removed.CapabilityVerificationSource;
        }

        if (string.IsNullOrWhiteSpace(survivor.CapabilityVerificationSource))
        {
            survivor.CapabilityVerificationSource = removed.CapabilityVerificationSource;
        }
    }

    private static void AddHistoricalIdentity(
        KnownPhysicalDisplay known,
        DisplayIdentity identity)
    {
        if (known.PhysicalIdentity.Equals(identity)
            || known.HistoricalIdentities.Any(existing => existing.Equals(identity)))
        {
            return;
        }

        known.HistoricalIdentities.Add(identity);
    }

    private static bool RepointReferences(
        DisplaySettings settings,
        string duplicateId,
        KnownPhysicalDisplay replacement)
    {
        if (string.IsNullOrWhiteSpace(duplicateId)
            || string.Equals(duplicateId, replacement.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var changed = false;
        var canonicalIdentity = CanonicalIdentity(replacement);
        foreach (var profile in settings.GamingProfiles ?? [])
        {
            foreach (var target in profile.DisplayTopology?.Displays ?? [])
            {
                if (!string.Equals(target.KnownPhysicalDisplayId, duplicateId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                target.KnownPhysicalDisplayId = replacement.Id;
                target.Identity = canonicalIdentity;
                changed = true;
            }

            if (profile.Layout is not null)
            {
                var layoutChanged = false;
                var displays = profile.Layout.Displays.Select(display =>
                {
                    if (!string.Equals(display.KnownPhysicalDisplayId, duplicateId, StringComparison.OrdinalIgnoreCase))
                    {
                        return display;
                    }

                    changed = true;
                    layoutChanged = true;
                    return new DisplayPathSnapshot
                    {
                        KnownPhysicalDisplayId = replacement.Id,
                        Identity = canonicalIdentity,
                        IsActive = display.IsActive,
                        IsPrimary = display.IsPrimary,
                        PositionX = display.PositionX,
                        PositionY = display.PositionY,
                        Width = display.Width,
                        Height = display.Height,
                        Orientation = display.Orientation,
                        RefreshRateNumerator = display.RefreshRateNumerator,
                        RefreshRateDenominator = display.RefreshRateDenominator,
                        SourceId = display.SourceId,
                        TargetId = display.TargetId,
                        AdapterLuid = display.AdapterLuid,
                        SourceDeviceName = display.SourceDeviceName,
                        ConnectionTechnology = display.ConnectionTechnology
                    };
                }).ToList();
                if (layoutChanged)
                {
                    profile.Layout = new DisplayLayoutSnapshot
                    {
                        Id = profile.Layout.Id,
                        CapturedAt = profile.Layout.CapturedAt,
                        Displays = displays
                    };
                }
            }
        }

        if (settings.GamingLayout is not null)
        {
            var gamingLayoutChanged = false;
            var displays = settings.GamingLayout.Displays.Select(display =>
            {
                if (!string.Equals(display.KnownPhysicalDisplayId, duplicateId, StringComparison.OrdinalIgnoreCase))
                {
                    return display;
                }

                changed = true;
                gamingLayoutChanged = true;
                return new DisplayPathSnapshot
                {
                    KnownPhysicalDisplayId = replacement.Id,
                    Identity = canonicalIdentity,
                    IsActive = display.IsActive,
                    IsPrimary = display.IsPrimary,
                    PositionX = display.PositionX,
                    PositionY = display.PositionY,
                    Width = display.Width,
                    Height = display.Height,
                    Orientation = display.Orientation,
                    RefreshRateNumerator = display.RefreshRateNumerator,
                    RefreshRateDenominator = display.RefreshRateDenominator,
                    SourceId = display.SourceId,
                    TargetId = display.TargetId,
                    AdapterLuid = display.AdapterLuid,
                    SourceDeviceName = display.SourceDeviceName,
                    ConnectionTechnology = display.ConnectionTechnology
                };
            }).ToList();
            if (gamingLayoutChanged)
            {
                settings.GamingLayout = new DisplayLayoutSnapshot
                {
                    Id = settings.GamingLayout.Id,
                    CapturedAt = settings.GamingLayout.CapturedAt,
                    Displays = displays
                };
            }
        }

        return changed;
    }

    private static string ResolveFriendlyName(KnownPhysicalDisplay known)
    {
        return !string.IsNullOrWhiteSpace(known.OptionalUserLabel)
            ? known.OptionalUserLabel
            : ResolveCanonicalFriendlyName(known);
    }

    private static string ResolveCanonicalFriendlyName(KnownPhysicalDisplay known)
    {
        return string.IsNullOrWhiteSpace(known.CanonicalFriendlyName)
            ? known.PhysicalIdentity.FriendlyName
            : known.CanonicalFriendlyName;
    }

    private static bool EnsureCanonicalName(KnownPhysicalDisplay known, string? candidateName)
    {
        if (!string.IsNullOrWhiteSpace(known.CanonicalFriendlyName)
            || string.IsNullOrWhiteSpace(candidateName))
        {
            return false;
        }

        known.CanonicalFriendlyName = candidateName.Trim();
        return true;
    }

    private static bool PromoteRicherPhysicalIdentity(KnownPhysicalDisplay known)
    {
        var current = known.PhysicalIdentity;
        var preferred = new[] { current }
            .Concat(known.HistoricalIdentities ?? [])
            .OrderByDescending(GetIdentityMetadataScore)
            .First();
        if (ReferenceEquals(preferred, current) || Equals(preferred, current))
        {
            return false;
        }

        var canonicalName = string.IsNullOrWhiteSpace(known.CanonicalFriendlyName)
            ? preferred.FriendlyName
            : known.CanonicalFriendlyName;
        known.PhysicalIdentity = preferred with
        {
            FriendlyName = canonicalName
        };
        AddHistoricalIdentity(known, current);
        return true;
    }

    private static int GetIdentityMetadataScore(DisplayIdentity identity)
    {
        return (identity.AdapterLuid != 0 && identity.TargetId != 0 ? 4 : 0)
            + (!string.IsNullOrWhiteSpace(identity.MonitorDevicePath) ? 3 : 0)
            + (HasValidEdid(identity) ? 3 : 0)
            + (identity.ConnectorInstance != 0 ? 1 : 0)
            + (!string.IsNullOrWhiteSpace(identity.OutputTechnology) ? 1 : 0);
    }

    private static string ResolveFriendlyName(DisplayDeviceInfo display)
    {
        return string.IsNullOrWhiteSpace(display.Name) ? display.Identity.FriendlyName : display.Name;
    }

    private static bool NormalizeIdentityName(
        DisplaySettings settings,
        DisplayIdentity identity,
        Action<DisplayIdentity> replace)
    {
        var known = FindKnown(settings, identity);
        if (known is null)
        {
            return false;
        }

        var canonicalIdentity = CanonicalIdentity(known);
        if (Equals(identity, canonicalIdentity))
        {
            return false;
        }

        replace(canonicalIdentity);
        return true;
    }

    private static bool NormalizeTargetReference(DisplaySettings settings, ProfileDisplayTarget target)
    {
        var known = FindKnownById(settings, target.KnownPhysicalDisplayId)
            ?? FindKnown(settings, target.Identity);
        if (known is null)
        {
            return false;
        }

        var changed = false;
        if (!string.Equals(target.KnownPhysicalDisplayId, known.Id, StringComparison.Ordinal))
        {
            target.KnownPhysicalDisplayId = known.Id;
            changed = true;
        }

        var canonicalIdentity = CanonicalIdentity(known);
        if (!Equals(target.Identity, canonicalIdentity))
        {
            target.Identity = canonicalIdentity;
            changed = true;
        }

        return changed;
    }

    private static DisplayLayoutSnapshot NormalizeLayoutNames(
        DisplaySettings settings,
        DisplayLayoutSnapshot layout,
        ref bool changed)
    {
        var layoutChanged = false;
        var displays = layout.Displays.Select(display =>
        {
            var known = FindKnownById(settings, display.KnownPhysicalDisplayId)
                ?? FindKnown(settings, display.Identity);
            if (known is null)
            {
                return display;
            }

            var canonicalIdentity = CanonicalIdentity(known);
            if (Equals(display.Identity, canonicalIdentity)
                && string.Equals(display.KnownPhysicalDisplayId, known.Id, StringComparison.Ordinal))
            {
                return display;
            }

            layoutChanged = true;
            return new DisplayPathSnapshot
            {
                KnownPhysicalDisplayId = known.Id,
                Identity = canonicalIdentity,
                IsActive = display.IsActive,
                IsPrimary = display.IsPrimary,
                PositionX = display.PositionX,
                PositionY = display.PositionY,
                Width = display.Width,
                Height = display.Height,
                Orientation = display.Orientation,
                RefreshRateNumerator = display.RefreshRateNumerator,
                RefreshRateDenominator = display.RefreshRateDenominator,
                SourceId = display.SourceId,
                TargetId = display.TargetId,
                AdapterLuid = display.AdapterLuid,
                SourceDeviceName = display.SourceDeviceName,
                ConnectionTechnology = display.ConnectionTechnology
            };
        }).ToList();

        changed |= layoutChanged;

        return !layoutChanged
            ? layout
            : new DisplayLayoutSnapshot
            {
                Id = layout.Id,
                CapturedAt = layout.CapturedAt,
                Displays = displays
            };
    }

    private static string Prefer(string existing, string candidate)
    {
        return string.IsNullOrWhiteSpace(existing) ? candidate : existing;
    }

    private static bool HasPhysicalIdentity(DisplayIdentity identity)
    {
        return identity.AdapterLuid != 0 && identity.TargetId != 0
            || !string.IsNullOrWhiteSpace(identity.MonitorDevicePath)
            || identity.EdidIdsValid && identity.EdidManufacturerId != 0 && identity.EdidProductCodeId != 0;
    }

    private static bool HasValidEdid(DisplayIdentity identity)
    {
        return identity.EdidIdsValid
            && identity.EdidManufacturerId != 0
            && identity.EdidProductCodeId != 0;
    }

    private static string NewKnownDisplayId() => Guid.NewGuid().ToString("N");

    private static string BuildIdentityKey(DisplayIdentity identity)
    {
        if (identity.AdapterLuid != 0 && identity.TargetId != 0)
        {
            return $"target:{identity.AdapterLuid}:{identity.TargetId}";
        }

        if (!string.IsNullOrWhiteSpace(identity.MonitorDevicePath))
        {
            return $"path:{identity.MonitorDevicePath.Trim().ToLowerInvariant()}";
        }

        return $"edid:{identity.EdidManufacturerId}:{identity.EdidProductCodeId}:{identity.ConnectorInstance}";
    }
}

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
    IReadOnlyList<DisplayIdentityMatchRecord> IdentityMatches);

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
