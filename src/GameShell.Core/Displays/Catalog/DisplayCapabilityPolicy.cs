using GameShell.Core.Settings;

namespace GameShell.Core.Displays;

internal static class DisplayCapabilityPolicy
{
    internal static bool IsCapabilityRegression(DisplayCapabilitySnapshot? current, DisplayCapabilitySnapshot? known)
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

    internal static DisplayCapabilitySnapshot? CreateCapability(DisplayDeviceInfo display)
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

    internal static bool IsBetterCapability(DisplayCapabilitySnapshot? candidate, DisplayCapabilitySnapshot? existing)
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

    internal static bool IsValidCapability(DisplayCapabilityExpectation? capability)
    {
        return capability is not null
            && capability.Width > 0
            && capability.Height > 0
            && capability.LastKnownGoodMaxRefreshNumerator > 0
            && capability.LastKnownGoodMaxRefreshDenominator > 0
            && double.IsFinite(capability.MaxRefreshRateHz);
    }

    internal static DisplayCapabilitySnapshot ToSnapshot(DisplayCapabilityExpectation capability)
    {
        return new DisplayCapabilitySnapshot
        {
            Width = capability.Width,
            Height = capability.Height,
            MaxRefreshRateNumerator = capability.LastKnownGoodMaxRefreshNumerator,
            MaxRefreshRateDenominator = capability.LastKnownGoodMaxRefreshDenominator
        };
    }

    internal static DisplayCapabilitySnapshot? GetBestKnown(KnownPhysicalDisplay known)
    {
        return known.BestKnownCapability ?? known.LastKnownGoodCapability;
    }

    internal static DisplayCapabilitySnapshot? GetVerifiedActive(KnownPhysicalDisplay known)
    {
        return known.LastVerifiedActiveCapability ?? GetBestKnown(known);
    }
}
