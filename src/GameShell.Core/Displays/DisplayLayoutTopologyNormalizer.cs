
namespace GameShell.Core.Displays;

public static class DisplayLayoutTopologyNormalizer
{
    public static DisplayLayoutSnapshot NormalizePrimaryOrigin(DisplayLayoutSnapshot snapshot)
    {
        var primary = snapshot.Displays.FirstOrDefault(display => display.IsActive && display.IsPrimary);
        if (primary is null || (primary.PositionX == 0 && primary.PositionY == 0))
        {
            return snapshot;
        }

        return new DisplayLayoutSnapshot
        {
            Id = snapshot.Id,
            CapturedAt = snapshot.CapturedAt,
            Displays = snapshot.Displays.Select(display => new DisplayPathSnapshot
            {
                KnownPhysicalDisplayId = display.KnownPhysicalDisplayId,
                Identity = display.Identity,
                IsActive = display.IsActive,
                IsPrimary = display.IsPrimary,
                PositionX = checked(display.PositionX - primary.PositionX),
                PositionY = checked(display.PositionY - primary.PositionY),
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
            }).ToList()
        };
    }
}
