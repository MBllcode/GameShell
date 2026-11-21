using GameShell.Core.Settings;

namespace GameShell.Core.Displays;

internal static class DisplayCatalogReconciliation
{
    public static bool NormalizeCanonicalNames(DisplaySettings settings)
    {
        settings.ConsoleDisplays ??= [];
        var changed = false;

        foreach (var profile in settings.GamingProfiles ?? [])
        {
            if (profile.DisplayTopology is { } topology)
            {
                foreach (var target in topology.Displays)
                {
                    changed |= NormalizeTargetReference(settings, target);
                }

                var displays = CollapseProfileAliases(topology.Displays, display => display.KnownPhysicalDisplayId, group =>
                {
                    var retained = group.OrderByDescending(display => display.Enabled && display.IsPrimary)
                        .ThenByDescending(display => display.Enabled)
                        .ThenByDescending(display => HasResolution(display.Width, display.Height))
                        .First();
                    if (!HasResolution(retained.Width, retained.Height)
                        && group.FirstOrDefault(display => HasResolution(display.Width, display.Height)) is { } withResolution)
                    {
                        retained.Width = withResolution.Width;
                        retained.Height = withResolution.Height;
                    }

                    return retained;
                });
                if (displays.Count != topology.Displays.Count)
                {
                    topology.Displays = displays;
                    changed = true;
                }
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

        changed |= RewriteApplicationDisplayIds(settings, id => ResolveApplicationDisplayId(settings, id));
        return changed;
    }

    internal static bool NormalizeKnownDisplays(DisplaySettings settings)
    {
        var changed = false;
        var knownDisplays = settings.KnownPhysicalDisplays;
        var result = new List<KnownPhysicalDisplay>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in knownDisplays)
        {
            if (candidate is null || !DisplayCatalogIdentity.HasPhysicalIdentity(candidate.PhysicalIdentity))
            {
                changed = true;
                continue;
            }

            candidate.HistoricalIdentities ??= [];
            changed |= DisplayCatalogIdentity.PromoteRicherPhysicalIdentity(candidate);

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

            changed |= DisplayCatalogIdentity.EnsureCanonicalName(candidate, candidate.PhysicalIdentity.FriendlyName);

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

    internal static KnownPhysicalDisplay ChooseSurvivor(
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

    internal static int GetKnownDisplayRichness(KnownPhysicalDisplay known)
    {
        var score = (known.PhysicalIdentity.AdapterLuid != 0 ? 1 : 0)
            + (known.PhysicalIdentity.TargetId != 0 ? 1 : 0)
            + (!string.IsNullOrWhiteSpace(known.PhysicalIdentity.MonitorDevicePath) ? 2 : 0)
            + (DisplayCatalogIdentity.HasValidEdid(known.PhysicalIdentity) ? 2 : 0)
            + (known.PhysicalIdentity.ConnectorInstance != 0 ? 1 : 0)
            + (known.HistoricalIdentities?.Count ?? 0) * 2;
        if (DisplayCapabilityPolicy.GetBestKnown(known) is not null)
        {
            score += 3;
        }

        if (DisplayCapabilityPolicy.GetVerifiedActive(known) is not null)
        {
            score += 2;
        }

        return score;
    }

    internal static bool IsKnownDisplayReferenced(DisplaySettings settings, string knownId)
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

    internal static void MergeKnownData(
        KnownPhysicalDisplay survivor,
        KnownPhysicalDisplay removed)
    {
        survivor.HistoricalIdentities ??= [];
        DisplayCatalogIdentity.PromoteModelAlias(survivor, removed.PhysicalIdentity);
        DisplayCatalogIdentity.AddHistoricalIdentity(survivor, removed.PhysicalIdentity);
        foreach (var identity in removed.HistoricalIdentities ?? [])
        {
            DisplayCatalogIdentity.AddHistoricalIdentity(survivor, identity);
        }

        survivor.PhysicalIdentity = DisplayCatalogIdentity.EnrichIdentity(
            survivor.PhysicalIdentity,
            removed.PhysicalIdentity,
            DisplayCatalogIdentity.ResolveFriendlyName(survivor));
        if (string.IsNullOrWhiteSpace(survivor.CanonicalFriendlyName))
        {
            survivor.CanonicalFriendlyName = removed.CanonicalFriendlyName;
        }

        if (string.IsNullOrWhiteSpace(survivor.OptionalUserLabel))
        {
            survivor.OptionalUserLabel = removed.OptionalUserLabel;
        }

        if (DisplayCapabilityPolicy.IsBetterCapability(DisplayCapabilityPolicy.GetBestKnown(removed), DisplayCapabilityPolicy.GetBestKnown(survivor)))
        {
            survivor.BestKnownCapability = DisplayCapabilityPolicy.GetBestKnown(removed);
        }

        if (DisplayCapabilityPolicy.IsBetterCapability(DisplayCapabilityPolicy.GetVerifiedActive(removed), DisplayCapabilityPolicy.GetVerifiedActive(survivor)))
        {
            survivor.LastVerifiedActiveCapability = DisplayCapabilityPolicy.GetVerifiedActive(removed);
            survivor.CapabilityVerificationSource = removed.CapabilityVerificationSource;
        }

        if (string.IsNullOrWhiteSpace(survivor.CapabilityVerificationSource))
        {
            survivor.CapabilityVerificationSource = removed.CapabilityVerificationSource;
        }
    }

    internal static bool RepointReferences(
        DisplaySettings settings,
        string duplicateId,
        KnownPhysicalDisplay replacement)
    {
        if (string.IsNullOrWhiteSpace(duplicateId)
            || string.Equals(duplicateId, replacement.Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var changed = RewriteApplicationDisplayIds(settings, id =>
            string.Equals(id, duplicateId, StringComparison.OrdinalIgnoreCase) ? replacement.Id : null);
        var canonicalIdentity = DisplayCatalogIdentity.CanonicalIdentity(replacement);
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
                    return CopyLayoutDisplay(display, replacement.Id, canonicalIdentity);
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
                return CopyLayoutDisplay(display, replacement.Id, canonicalIdentity);
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

    private static bool RewriteApplicationDisplayIds(DisplaySettings settings, Func<string, string?> resolveDisplayId)
    {
        var changed = false;
        foreach (var configuration in settings.GamingProfileConfigurations ?? [])
        {
            var applications = configuration.Applications;
            if (applications is null) continue;

            for (var index = 0; index < applications.Count; index++)
            {
                var application = applications[index];
                if (application is null || string.IsNullOrWhiteSpace(application.DisplayId)) continue;

                var replacementId = resolveDisplayId(application.DisplayId);
                if (replacementId is not null && !string.Equals(replacementId, application.DisplayId, StringComparison.Ordinal))
                {
                    applications[index] = application with { DisplayId = replacementId };
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static string? ResolveApplicationDisplayId(DisplaySettings settings, string id)
    {
        var known = DisplayCatalogIdentity.FindKnownById(settings, id);
        if (known is not null) return known.Id;

        // Legacy assignments contain the Windows device path, which may now be an alias.
        var matches = settings.KnownPhysicalDisplays.Where(candidate =>
            string.Equals(candidate.PhysicalIdentity.MonitorDevicePath, id, StringComparison.OrdinalIgnoreCase)
            || (candidate.HistoricalIdentities ?? []).Any(identity =>
                string.Equals(identity.MonitorDevicePath, id, StringComparison.OrdinalIgnoreCase)))
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }

    internal static bool NormalizeIdentityName(
        DisplaySettings settings,
        DisplayIdentity identity,
        Action<DisplayIdentity> replace)
    {
        var known = DisplayCatalogIdentity.FindKnown(settings, identity);
        if (known is null)
        {
            return false;
        }

        var canonicalIdentity = DisplayCatalogIdentity.CanonicalIdentity(known);
        if (Equals(identity, canonicalIdentity))
        {
            return false;
        }

        replace(canonicalIdentity);
        return true;
    }

    internal static bool NormalizeTargetReference(DisplaySettings settings, ProfileDisplayTarget target)
    {
        var known = DisplayCatalogIdentity.FindKnownById(settings, target.KnownPhysicalDisplayId)
            ?? DisplayCatalogIdentity.FindKnown(settings, target.Identity);
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

        var canonicalIdentity = DisplayCatalogIdentity.CanonicalIdentity(known);
        if (!Equals(target.Identity, canonicalIdentity))
        {
            target.Identity = canonicalIdentity;
            changed = true;
        }

        return changed;
    }

    internal static DisplayLayoutSnapshot NormalizeLayoutNames(
        DisplaySettings settings,
        DisplayLayoutSnapshot layout,
        ref bool changed)
    {
        var layoutChanged = false;
        var displays = layout.Displays.Select(display =>
        {
            var known = DisplayCatalogIdentity.FindKnownById(settings, display.KnownPhysicalDisplayId)
                ?? DisplayCatalogIdentity.FindKnown(settings, display.Identity);
            if (known is null)
            {
                return display;
            }

            var canonicalIdentity = DisplayCatalogIdentity.CanonicalIdentity(known);
            if (Equals(display.Identity, canonicalIdentity)
                && string.Equals(display.KnownPhysicalDisplayId, known.Id, StringComparison.Ordinal))
            {
                return display;
            }

            layoutChanged = true;
            return CopyLayoutDisplay(display, known.Id, canonicalIdentity);
        }).ToList();

        var collapsed = CollapseProfileAliases(displays, display => display.KnownPhysicalDisplayId, group =>
        {
            var retained = group.OrderByDescending(display => display.IsActive && display.IsPrimary)
                .ThenByDescending(display => display.IsActive)
                .ThenByDescending(display => HasResolution(display.Width, display.Height))
                .First();
            var withResolution = group.FirstOrDefault(display => HasResolution(display.Width, display.Height));
            if (HasResolution(retained.Width, retained.Height) || withResolution is null)
            {
                return retained;
            }

            return CopyLayoutDisplay(retained, retained.KnownPhysicalDisplayId, retained.Identity, withResolution);
        });
        layoutChanged |= collapsed.Count != displays.Count;
        changed |= layoutChanged;

        return !layoutChanged
            ? layout
            : new DisplayLayoutSnapshot
            {
                Id = layout.Id,
                CapturedAt = layout.CapturedAt,
                Displays = collapsed
            };
    }

    private static List<T> CollapseProfileAliases<T>(
        List<T> displays, Func<T, string> knownId, Func<IEnumerable<T>, T> choose)
    {
        return displays.Select((display, index) => (Display: display, Id: knownId(display), Index: index))
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Id)
                ? (Id: string.Empty, item.Index)
                : (Id: item.Id.ToUpperInvariant(), Index: -1))
            .Select(group => group.Count() == 1 ? group.First().Display : choose(group.Select(item => item.Display)))
            .ToList();
    }

    private static bool HasResolution(int width, int height) => width > 0 && height > 0;

    private static DisplayPathSnapshot CopyLayoutDisplay(
        DisplayPathSnapshot display, string knownId, DisplayIdentity identity, DisplayPathSnapshot? resolution = null)
    {
        return new DisplayPathSnapshot
        {
            KnownPhysicalDisplayId = knownId,
            Identity = identity,
            IsActive = display.IsActive,
            IsPrimary = display.IsPrimary,
            PositionX = display.PositionX,
            PositionY = display.PositionY,
            Width = resolution?.Width ?? display.Width,
            Height = resolution?.Height ?? display.Height,
            Orientation = display.Orientation,
            RefreshRateNumerator = display.RefreshRateNumerator,
            RefreshRateDenominator = display.RefreshRateDenominator,
            SourceId = display.SourceId,
            TargetId = display.TargetId,
            AdapterLuid = display.AdapterLuid,
            SourceDeviceName = display.SourceDeviceName,
            ConnectionTechnology = display.ConnectionTechnology
        };
    }
}
