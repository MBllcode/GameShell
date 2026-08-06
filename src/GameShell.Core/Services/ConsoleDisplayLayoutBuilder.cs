using GameShell.Core.Models;

namespace GameShell.Core.Services;

public sealed class ConsoleDisplayLayoutBuilder
{
    public DisplayLayoutSnapshot Build(DisplayLayoutSnapshot currentLayout, DisplayIdentity consoleDisplay)
    {
        return Build(currentLayout, [consoleDisplay]);
    }

    public DisplayLayoutSnapshot Build(DisplayLayoutSnapshot currentLayout, IReadOnlyCollection<DisplayIdentity> consoleDisplays)
    {
        var selected = currentLayout.Displays
            .Where(display => display.IsActive)
            .Where(display => consoleDisplays.Any(consoleDisplay => DisplayIdentityMatcher.IsReliableMatch(consoleDisplay, display.Identity)))
            .OrderBy(display => display.PositionX)
            .ThenBy(display => display.PositionY)
            .ToList();

        if (selected.Count == 0)
        {
            throw new InvalidOperationException("Wybrane ekrany Console Mode nie są obecnie dostępne.");
        }

        var primary = selected.FirstOrDefault(display => display.IsPrimary) ?? selected[0];
        var originX = primary.PositionX;
        var originY = primary.PositionY;

        return new DisplayLayoutSnapshot
        {
            CapturedAt = DateTimeOffset.UtcNow,
            Displays = selected.Select(display => new DisplayPathSnapshot
            {
                Identity = display.Identity,
                IsActive = true,
                IsPrimary = DisplayIdentityMatcher.IsReliableMatch(primary.Identity, display.Identity),
                PositionX = display.PositionX - originX,
                PositionY = display.PositionY - originY,
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

