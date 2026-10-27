
namespace GameShell.Core.Displays;

public static class DisplayIdentityMatcher
{
    public static DisplayIdentityMatchResult ResolveKnownDisplay(
        KnownPhysicalDisplay known,
        DisplayIdentity detected)
    {
        var sources = new[] { known.PhysicalIdentity }
            .Concat(known.HistoricalIdentities ?? [])
            .Where(HasIdentityEvidence)
            .ToList();

        var matches = sources
            .Select((source, index) => new
            {
                Source = source,
                Index = index,
                Confidence = GetMatchConfidence(source, detected),
                Score = GetStableIdentityScore(source, detected)
            })
            .Where(candidate => IsRawReliableMatch(candidate.Confidence))
            .OrderByDescending(candidate => GetConfidenceRank(candidate.Confidence))
            .ThenByDescending(candidate => candidate.Score)
            .ToList();

        // A stale historical alias may contain an old EDID/product value while
        // another accumulated alias matches the current route/path exactly.
        // The positive strong match is the evidence for this physical display;
        // an unrelated stale alias must not turn that match into a conflict.
        if (matches.Count > 0)
        {
            var match = matches[0];
            var historical = match.Index > 0;
            var confidence = historical && match.Confidence == DisplayIdentityMatchConfidence.AdapterAndTargetRoute
                ? DisplayIdentityMatchConfidence.HistoricalRouteAlias
                : match.Confidence;
            return new DisplayIdentityMatchResult(
                confidence,
                true,
                false,
                historical,
                HasSameMonitorPath(match.Source, detected),
                HasSameEdid(match.Source, detected),
                HasMissingMetadata(match.Source, detected));
        }

        var conflicts = sources
            .Select(source => GetMatchConfidence(source, detected))
            .Any(confidence => confidence == DisplayIdentityMatchConfidence.IdentityConflict);
        if (conflicts)
        {
            return new DisplayIdentityMatchResult(
                DisplayIdentityMatchConfidence.IdentityConflict,
                false,
                true,
                false,
                HasSameMonitorPath(known.PhysicalIdentity, detected),
                HasSameEdid(known.PhysicalIdentity, detected),
                HasMissingMetadata(known.PhysicalIdentity, detected));
        }

        return new DisplayIdentityMatchResult(
            DisplayIdentityMatchConfidence.None,
            false,
            false,
            false,
            HasSameMonitorPath(known.PhysicalIdentity, detected),
            HasSameEdid(known.PhysicalIdentity, detected),
            HasMissingMetadata(known.PhysicalIdentity, detected));
    }

    public static bool IsReliableMatch(KnownPhysicalDisplay known, DisplayIdentity detected)
    {
        return ResolveKnownDisplay(known, detected).IsMatch;
    }

    public static bool CanReconcileKnownDisplays(
        KnownPhysicalDisplay left,
        KnownPhysicalDisplay right)
    {
        if (ResolveKnownDisplay(left, right.PhysicalIdentity).IsMatch
            || ResolveKnownDisplay(right, left.PhysicalIdentity).IsMatch)
        {
            return true;
        }

        var leftSources = new[] { left.PhysicalIdentity }
            .Concat(left.HistoricalIdentities ?? []);
        var rightSources = new[] { right.PhysicalIdentity }
            .Concat(right.HistoricalIdentities ?? []);

        var oneHasCapability = left.BestKnownCapability is not null;
        var otherHasCapability = right.BestKnownCapability is not null;
        return oneHasCapability != otherHasCapability
            && leftSources.Any(leftIdentity => rightSources.Any(rightIdentity =>
                IsCompatibleRouteFamily(leftIdentity, rightIdentity)));
    }

    public static DisplayIdentityMatchConfidence GetMatchConfidence(DisplayIdentity saved, DisplayIdentity detected)
    {
        if (HasConflictingStrongMetadata(saved, detected))
        {
            return DisplayIdentityMatchConfidence.IdentityConflict;
        }

        if (HasSameAdapterAndTarget(saved, detected))
        {
            return DisplayIdentityMatchConfidence.AdapterAndTargetRoute;
        }

        if (!string.IsNullOrWhiteSpace(saved.MonitorDevicePath)
            && !string.IsNullOrWhiteSpace(detected.MonitorDevicePath)
            && string.Equals(saved.MonitorDevicePath, detected.MonitorDevicePath, StringComparison.OrdinalIgnoreCase))
        {
            return DisplayIdentityMatchConfidence.ExactMonitorDevicePath;
        }

        var hasSavedEdid = saved.EdidIdsValid && saved.EdidManufacturerId != 0 && saved.EdidProductCodeId != 0;
        var hasDetectedEdid = detected.EdidIdsValid && detected.EdidManufacturerId != 0 && detected.EdidProductCodeId != 0;
        if (hasSavedEdid
            && hasDetectedEdid
            && saved.EdidManufacturerId == detected.EdidManufacturerId
            && saved.EdidProductCodeId == detected.EdidProductCodeId)
        {
            return saved.ConnectorInstance != 0
                && detected.ConnectorInstance != 0
                && saved.ConnectorInstance == detected.ConnectorInstance
                    ? DisplayIdentityMatchConfidence.EdidProductAndConnector
                    : DisplayIdentityMatchConfidence.EdidProduct;
        }

        if (!string.IsNullOrWhiteSpace(saved.FriendlyName)
            && !string.IsNullOrWhiteSpace(detected.FriendlyName)
            && string.Equals(saved.FriendlyName, detected.FriendlyName, StringComparison.OrdinalIgnoreCase))
        {
            return DisplayIdentityMatchConfidence.WeakFriendlyName;
        }

        return DisplayIdentityMatchConfidence.None;
    }

    public static bool IsReliableMatch(DisplayIdentity saved, DisplayIdentity detected)
    {
        return IsReliableMatch(GetMatchConfidence(saved, detected));
    }

    public static bool IsReliableMatch(DisplayIdentityMatchConfidence confidence)
    {
        return confidence is DisplayIdentityMatchConfidence.AdapterAndTargetRoute
            or DisplayIdentityMatchConfidence.HistoricalRouteAlias
            or DisplayIdentityMatchConfidence.ExactMonitorDevicePath
            or DisplayIdentityMatchConfidence.EdidProduct
            or DisplayIdentityMatchConfidence.EdidProductAndConnector;
    }

    public static bool IsSamePhysicalDisplay(DisplayIdentity left, DisplayIdentity right)
    {
        return IsReliableMatch(left, right);
    }

    public static int GetStableIdentityScore(DisplayIdentity saved, DisplayIdentity detected)
    {
        var score = 0;
        if (saved.AdapterLuid != 0 && saved.AdapterLuid == detected.AdapterLuid)
        {
            score += 40;
        }

        if (saved.TargetId != 0 && saved.TargetId == detected.TargetId)
        {
            score += saved.AdapterLuid != 0 && saved.AdapterLuid == detected.AdapterLuid ? 45 : 10;
        }

        if (saved.ConnectorInstance != 0 && saved.ConnectorInstance == detected.ConnectorInstance)
        {
            score += 25;
        }

        if (!string.IsNullOrWhiteSpace(saved.OutputTechnology)
            && !string.IsNullOrWhiteSpace(detected.OutputTechnology)
            && string.Equals(saved.OutputTechnology, detected.OutputTechnology, StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }

        if (saved.EdidIdsValid
            && detected.EdidIdsValid
            && saved.EdidManufacturerId != 0
            && saved.EdidProductCodeId != 0
            && saved.EdidManufacturerId == detected.EdidManufacturerId
            && saved.EdidProductCodeId == detected.EdidProductCodeId)
        {
            score += 35;
        }

        if (!string.IsNullOrWhiteSpace(saved.MonitorDevicePath)
            && !string.IsNullOrWhiteSpace(detected.MonitorDevicePath)
            && string.Equals(saved.MonitorDevicePath, detected.MonitorDevicePath, StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        return score;
    }

    public static int GetStableIdentityScore(KnownPhysicalDisplay known, DisplayIdentity detected)
    {
        return new[] { known.PhysicalIdentity }
            .Concat(known.HistoricalIdentities ?? [])
            .Select(identity => GetStableIdentityScore(identity, detected))
            .DefaultIfEmpty()
            .Max();
    }

    public static DisplayIdentityMatchConfidence GetMatchConfidence(KnownPhysicalDisplay known, DisplayIdentity detected)
    {
        return ResolveKnownDisplay(known, detected).MatchConfidence;
    }

    private static bool IsRawReliableMatch(DisplayIdentityMatchConfidence confidence)
    {
        return confidence is DisplayIdentityMatchConfidence.AdapterAndTargetRoute
            or DisplayIdentityMatchConfidence.ExactMonitorDevicePath
            or DisplayIdentityMatchConfidence.EdidProduct
            or DisplayIdentityMatchConfidence.EdidProductAndConnector;
    }

    private static int GetConfidenceRank(DisplayIdentityMatchConfidence confidence)
    {
        return confidence switch
        {
            DisplayIdentityMatchConfidence.ExactMonitorDevicePath => 5,
            DisplayIdentityMatchConfidence.EdidProductAndConnector => 4,
            DisplayIdentityMatchConfidence.EdidProduct => 3,
            DisplayIdentityMatchConfidence.AdapterAndTargetRoute => 2,
            _ => 0
        };
    }

    private static bool HasIdentityEvidence(DisplayIdentity identity)
    {
        return identity.AdapterLuid != 0 && identity.TargetId != 0
            || !string.IsNullOrWhiteSpace(identity.MonitorDevicePath)
            || HasValidEdid(identity);
    }

    private static bool IsCompatibleRouteFamily(DisplayIdentity left, DisplayIdentity right)
    {
        if (left.AdapterLuid == 0
            || right.AdapterLuid == 0
            || left.AdapterLuid == right.AdapterLuid
            || left.TargetId == 0
            || left.TargetId != right.TargetId)
        {
            return false;
        }

        var sameManufacturer = HasValidEdid(left)
            && HasValidEdid(right)
            && left.EdidManufacturerId == right.EdidManufacturerId;
        var sameTechnology = !string.IsNullOrWhiteSpace(left.OutputTechnology)
            && !string.IsNullOrWhiteSpace(right.OutputTechnology)
            && string.Equals(left.OutputTechnology, right.OutputTechnology, StringComparison.OrdinalIgnoreCase);
        var sameStablePathShape = HasSameStableMonitorPath(left, right);

        // A route can survive a driver/session change with a new LUID. This
        // fallback requires compatible stable metadata; TargetId alone is not
        // sufficient and product-code changes are tolerated only as a degraded
        // observation of the same connector family.
        return sameManufacturer
            && sameTechnology
            && sameStablePathShape;
    }

    private static bool HasSameStableMonitorPath(DisplayIdentity left, DisplayIdentity right)
    {
        if (string.IsNullOrWhiteSpace(left.MonitorDevicePath)
            || string.IsNullOrWhiteSpace(right.MonitorDevicePath))
        {
            return false;
        }

        return string.Equals(
            GetStableMonitorPath(left.MonitorDevicePath),
            GetStableMonitorPath(right.MonitorDevicePath),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetStableMonitorPath(string path)
    {
        var parts = path.Split('#', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 4
            ? string.Join('#', parts[0], parts[2], string.Join('#', parts.Skip(3)))
            : path.Trim();
    }

    private static bool HasSameMonitorPath(DisplayIdentity saved, DisplayIdentity detected)
    {
        return !string.IsNullOrWhiteSpace(saved.MonitorDevicePath)
            && !string.IsNullOrWhiteSpace(detected.MonitorDevicePath)
            && string.Equals(saved.MonitorDevicePath, detected.MonitorDevicePath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSameEdid(DisplayIdentity saved, DisplayIdentity detected)
    {
        return HasValidEdid(saved)
            && HasValidEdid(detected)
            && saved.EdidManufacturerId == detected.EdidManufacturerId
            && saved.EdidProductCodeId == detected.EdidProductCodeId;
    }

    private static bool HasMissingMetadata(DisplayIdentity saved, DisplayIdentity detected)
    {
        return !string.IsNullOrWhiteSpace(saved.MonitorDevicePath) && string.IsNullOrWhiteSpace(detected.MonitorDevicePath)
            || HasValidEdid(saved) && !HasValidEdid(detected)
            || saved.AdapterLuid != 0 && detected.AdapterLuid == 0
            || saved.TargetId != 0 && detected.TargetId == 0;
    }

    private static bool HasSameAdapterAndTarget(DisplayIdentity saved, DisplayIdentity detected)
    {
        return saved.AdapterLuid != 0
            && detected.AdapterLuid != 0
            && saved.TargetId != 0
            && detected.TargetId != 0
            && saved.AdapterLuid == detected.AdapterLuid
            && saved.TargetId == detected.TargetId;
    }

    private static bool HasConflictingStrongMetadata(DisplayIdentity saved, DisplayIdentity detected)
    {
        var hasSameRoute = HasSameAdapterAndTarget(saved, detected);
        var hasSameMonitorPath = !string.IsNullOrWhiteSpace(saved.MonitorDevicePath)
            && !string.IsNullOrWhiteSpace(detected.MonitorDevicePath)
            && string.Equals(saved.MonitorDevicePath, detected.MonitorDevicePath, StringComparison.OrdinalIgnoreCase);

        // Different routes are ordinary different displays. Conflicting EDID
        // is only an identity conflict when a stronger route/path identity
        // says that both observations refer to the same physical target.
        if (!hasSameRoute && !hasSameMonitorPath)
        {
            return false;
        }

        var hasSavedEdid = HasValidEdid(saved);
        var hasDetectedEdid = HasValidEdid(detected);
        if (hasSavedEdid && hasDetectedEdid
            && (saved.EdidManufacturerId != detected.EdidManufacturerId
                || saved.EdidProductCodeId != detected.EdidProductCodeId))
        {
            return true;
        }

        // Monitor paths can be regenerated by Windows across a handshake or
        // topology change. Without contradictory EDID evidence, route identity
        // remains the stronger match and the path is only fallback metadata.
        return false;
    }

    private static bool HasValidEdid(DisplayIdentity identity)
    {
        return identity.EdidIdsValid
            && identity.EdidManufacturerId != 0
            && identity.EdidProductCodeId != 0;
    }
}

public sealed record DisplayIdentityMatchResult(
    DisplayIdentityMatchConfidence MatchConfidence,
    bool IsMatch,
    bool HasConflict,
    bool HistoricalRouteMatch,
    bool MonitorDevicePathMatch,
    bool EdidMatch,
    bool MissingMetadata);
