using GameShell.Core.Settings;

namespace GameShell.Core.Displays;

public static class GamingProfileDisplayTopologyAdapter
{
    public static GamingProfileDisplayTopology FromLegacy(DisplayLayoutSnapshot snapshot)
    {
        return new GamingProfileDisplayTopology
        {
            Displays = snapshot.Displays.Select(display => new ProfileDisplayTarget
            {
                KnownPhysicalDisplayId = display.KnownPhysicalDisplayId,
                Identity = display.Identity,
                Enabled = display.IsActive,
                IsPrimary = display.IsPrimary,
                PositionX = display.PositionX,
                PositionY = display.PositionY,
                Width = display.Width,
                Height = display.Height,
                RefreshPolicy = RefreshPolicy.AutoHighest,
                Orientation = display.Orientation
            }).ToList()
        };
    }

    public static DisplayLayoutSnapshot ToLegacy(GamingProfileDisplayTopology topology)
    {
        var snapshot = new DisplayLayoutSnapshot
        {
            CapturedAt = DateTimeOffset.UtcNow,
            Displays = topology.Displays.Select(display => new DisplayPathSnapshot
            {
                KnownPhysicalDisplayId = display.KnownPhysicalDisplayId,
                Identity = display.Identity,
                IsActive = display.Enabled,
                IsPrimary = display.IsPrimary,
                PositionX = display.PositionX,
                PositionY = display.PositionY,
                Width = display.Width,
                Height = display.Height,
                Orientation = display.Orientation,
                RefreshRateNumerator = display.EffectiveRefreshPolicy == RefreshPolicy.Fixed ? display.EffectiveFixedRefreshRateNumerator : 0,
                RefreshRateDenominator = display.EffectiveRefreshPolicy == RefreshPolicy.Fixed ? display.EffectiveFixedRefreshRateDenominator : 0
            }).ToList()
        };

        return DisplayLayoutTopologyNormalizer.NormalizePrimaryOrigin(snapshot);
    }
}
