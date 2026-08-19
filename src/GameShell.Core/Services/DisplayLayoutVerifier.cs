using GameShell.Core.Models;

namespace GameShell.Core.Services;

public static class DisplayLayoutVerifier
{
    public static bool IsConsoleLayout(DisplayLayoutSnapshot actual, IReadOnlyCollection<DisplayIdentity> consoleDisplays)
    {
        var active = actual.Displays.Where(display => display.IsActive).ToList();
        return active.Count == consoleDisplays.Count
            && active.Any(display => display.IsPrimary)
            && active.All(display => consoleDisplays.Any(consoleDisplay => DisplayIdentityMatcher.IsReliableMatch(consoleDisplay, display.Identity)));
    }

    public static bool MatchesSnapshot(DisplayLayoutSnapshot expected, DisplayLayoutSnapshot actual)
    {
        var expectedActive = expected.Displays.Where(display => display.IsActive).ToList();
        var actualActive = actual.Displays.Where(display => display.IsActive).ToList();
        if (expectedActive.Count != actualActive.Count)
        {
            return false;
        }

        if (expectedActive.Count == 0)
        {
            return true;
        }

        var expectedPrimary = expectedActive.FirstOrDefault(display => display.IsPrimary);
        if (expectedPrimary is not null)
        {
            var actualPrimary = actualActive.FirstOrDefault(display => display.IsPrimary);
            if (actualPrimary is null
                || !DisplayIdentityMatcher.IsReliableMatch(expectedPrimary.Identity, actualPrimary.Identity))
            {
                return false;
            }
        }

        int? offsetX = null;
        int? offsetY = null;

        foreach (var expectedDisplay in expectedActive)
        {
            var actualDisplay = actualActive.SingleOrDefault(display => DisplayIdentityMatcher.IsReliableMatch(expectedDisplay.Identity, display.Identity));
            if (actualDisplay is null)
            {
                return false;
            }

            offsetX ??= actualDisplay.PositionX - expectedDisplay.PositionX;
            offsetY ??= actualDisplay.PositionY - expectedDisplay.PositionY;

            if (actualDisplay.PositionX - expectedDisplay.PositionX != offsetX
                || actualDisplay.PositionY - expectedDisplay.PositionY != offsetY
                || actualDisplay.Width != expectedDisplay.Width
                || actualDisplay.Height != expectedDisplay.Height)
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesActiveDisplaySet(DisplayLayoutSnapshot expected, DisplayLayoutSnapshot actual)
    {
        var expectedActive = expected.Displays.Where(display => display.IsActive).ToList();
        var actualActive = actual.Displays.Where(display => display.IsActive).ToList();
        if (actualActive.Count < expectedActive.Count)
        {
            return false;
        }

        if (actualActive.Count > 0 && !actualActive.Any(display => display.IsPrimary))
        {
            return false;
        }

        var expectedPrimary = expectedActive.FirstOrDefault(display => display.IsPrimary);
        if (expectedPrimary is not null)
        {
            var actualPrimary = actualActive.FirstOrDefault(display => display.IsPrimary);
            if (actualPrimary is null
                || !DisplayIdentityMatcher.IsReliableMatch(expectedPrimary.Identity, actualPrimary.Identity))
            {
                return false;
            }
        }

        return expectedActive.All(expectedDisplay =>
            actualActive.Any(actualDisplay => DisplayIdentityMatcher.IsReliableMatch(expectedDisplay.Identity, actualDisplay.Identity)));
    }
}
