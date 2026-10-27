using GameShell.Core.Settings;

namespace GameShell.Core.Displays;

internal static class DisplayCatalogIdentity
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
        return known is null ? null : DisplayCapabilityPolicy.GetBestKnown(known);
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

    internal static KnownPhysicalDisplay? FindKnown(DisplaySettings settings, DisplayIdentity identity)
    {
        return FindKnownMatch(settings, identity)?.Known;
    }

    internal static KnownDisplayMatch? FindKnownMatch(DisplaySettings settings, DisplayIdentity identity)
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

    internal static KnownPhysicalDisplay? FindIdentityConflict(DisplaySettings settings, DisplayIdentity identity)
    {
        return settings.KnownPhysicalDisplays.FirstOrDefault(candidate =>
            DisplayIdentityMatcher.ResolveKnownDisplay(candidate, identity).HasConflict);
    }

    internal static DisplayIdentity CanonicalIdentity(KnownPhysicalDisplay known)
    {
        return known.PhysicalIdentity with { FriendlyName = ResolveCanonicalFriendlyName(known) };
    }

    internal static DisplayIdentity EnrichIdentity(DisplayIdentity existing, DisplayIdentity candidate, string canonicalName)
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

    internal static bool RecordIdentityObservation(
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

    internal static void AddHistoricalIdentity(
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

    internal static string ResolveFriendlyName(KnownPhysicalDisplay known)
    {
        return !string.IsNullOrWhiteSpace(known.OptionalUserLabel)
            ? known.OptionalUserLabel
            : ResolveCanonicalFriendlyName(known);
    }

    internal static string ResolveCanonicalFriendlyName(KnownPhysicalDisplay known)
    {
        return string.IsNullOrWhiteSpace(known.CanonicalFriendlyName)
            ? known.PhysicalIdentity.FriendlyName
            : known.CanonicalFriendlyName;
    }

    internal static bool EnsureCanonicalName(KnownPhysicalDisplay known, string? candidateName)
    {
        if (!string.IsNullOrWhiteSpace(known.CanonicalFriendlyName)
            || string.IsNullOrWhiteSpace(candidateName))
        {
            return false;
        }

        known.CanonicalFriendlyName = candidateName.Trim();
        return true;
    }

    internal static bool PromoteRicherPhysicalIdentity(KnownPhysicalDisplay known)
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

    internal static int GetIdentityMetadataScore(DisplayIdentity identity)
    {
        return (identity.AdapterLuid != 0 && identity.TargetId != 0 ? 4 : 0)
            + (!string.IsNullOrWhiteSpace(identity.MonitorDevicePath) ? 3 : 0)
            + (HasValidEdid(identity) ? 3 : 0)
            + (identity.ConnectorInstance != 0 ? 1 : 0)
            + (!string.IsNullOrWhiteSpace(identity.OutputTechnology) ? 1 : 0);
    }

    internal static string ResolveFriendlyName(DisplayDeviceInfo display)
    {
        return string.IsNullOrWhiteSpace(display.Name) ? display.Identity.FriendlyName : display.Name;
    }

    internal static string Prefer(string existing, string candidate)
    {
        return string.IsNullOrWhiteSpace(existing) ? candidate : existing;
    }

    internal static bool HasPhysicalIdentity(DisplayIdentity identity)
    {
        return identity.AdapterLuid != 0 && identity.TargetId != 0
            || !string.IsNullOrWhiteSpace(identity.MonitorDevicePath)
            || identity.EdidIdsValid && identity.EdidManufacturerId != 0 && identity.EdidProductCodeId != 0;
    }

    internal static bool HasStrongIdentity(DisplayIdentity identity)
    {
        return !string.IsNullOrWhiteSpace(identity.MonitorDevicePath)
            || HasValidEdid(identity);
    }

    internal static string ResolveCandidateCanonicalName(DisplaySettings settings, DisplayIdentity identity)
    {
        var candidate = settings.KnownPhysicalDisplays.FirstOrDefault(known => known.PhysicalIdentity.TargetId == identity.TargetId);
        return candidate is null ? identity.FriendlyName : ResolveFriendlyName(candidate);
    }

    internal static bool HasValidEdid(DisplayIdentity identity)
    {
        return identity.EdidIdsValid
            && identity.EdidManufacturerId != 0
            && identity.EdidProductCodeId != 0;
    }

    internal static string NewKnownDisplayId() => Guid.NewGuid().ToString("N");

    internal static string BuildIdentityKey(DisplayIdentity identity)
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
