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
            .FirstOrDefault(candidate => DisplayIdentityMatcher.IsSamePhysicalDisplay(candidate.PhysicalIdentity, identity));
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

    public static bool MigrateLegacyProfileCapabilities(DisplaySettings settings)
    {
        settings.KnownPhysicalDisplays ??= [];
        var changed = NormalizeKnownDisplays(settings.KnownPhysicalDisplays);

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
        var changed = NormalizeKnownDisplays(settings.KnownPhysicalDisplays);
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
        changed |= EnsureCanonicalName(known, physicalIdentity.FriendlyName);
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
        var changed = NormalizeKnownDisplays(settings.KnownPhysicalDisplays);
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
            var known = FindKnown(settings, current.Identity);
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
                    DisplayIdentityMatcher.GetMatchConfidence(known.PhysicalIdentity, current.Identity),
                    "Matched"));
                changed |= EnsureCanonicalName(known, ResolveFriendlyName(current));
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
        return settings.KnownPhysicalDisplays.FirstOrDefault(candidate =>
            DisplayIdentityMatcher.IsSamePhysicalDisplay(candidate.PhysicalIdentity, identity));
    }

    private static KnownPhysicalDisplay? FindIdentityConflict(DisplaySettings settings, DisplayIdentity identity)
    {
        return settings.KnownPhysicalDisplays.FirstOrDefault(candidate =>
            DisplayIdentityMatcher.GetMatchConfidence(candidate.PhysicalIdentity, identity)
                == DisplayIdentityMatchConfidence.IdentityConflict);
    }

    private static bool NormalizeKnownDisplays(List<KnownPhysicalDisplay> knownDisplays)
    {
        var changed = false;
        var result = new List<KnownPhysicalDisplay>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in knownDisplays)
        {
            if (candidate is null || !HasPhysicalIdentity(candidate.PhysicalIdentity))
            {
                changed = true;
                continue;
            }

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

            var existing = result.FirstOrDefault(item =>
                DisplayIdentityMatcher.IsSamePhysicalDisplay(item.PhysicalIdentity, candidate.PhysicalIdentity));
            if (existing is null)
            {
                result.Add(candidate);
                continue;
            }

            if (string.IsNullOrWhiteSpace(existing.CanonicalFriendlyName)
                && !string.IsNullOrWhiteSpace(candidate.CanonicalFriendlyName))
            {
                existing.CanonicalFriendlyName = candidate.CanonicalFriendlyName;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(existing.OptionalUserLabel)
                && !string.IsNullOrWhiteSpace(candidate.OptionalUserLabel))
            {
                existing.OptionalUserLabel = candidate.OptionalUserLabel;
                changed = true;
            }

            if (IsBetterCapability(GetBestKnown(candidate), GetBestKnown(existing)))
            {
                existing.BestKnownCapability = GetBestKnown(candidate);
            }

            if (IsBetterCapability(GetVerifiedActive(candidate), GetVerifiedActive(existing)))
            {
                existing.LastVerifiedActiveCapability = GetVerifiedActive(candidate);
                existing.CapabilityVerificationSource = candidate.CapabilityVerificationSource;
            }

            existing.PhysicalIdentity = EnrichIdentity(
                existing.PhysicalIdentity,
                candidate.PhysicalIdentity,
                ResolveFriendlyName(existing));
            changed = true;
        }

        if (changed)
        {
            knownDisplays.Clear();
            knownDisplays.AddRange(result);
        }

        return changed;
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
    string Result);

public sealed record CapabilityCatalogUpdate(
    DisplayCapabilitySnapshot? PreviousBest,
    DisplayCapabilitySnapshot? NewBest,
    string Action);
