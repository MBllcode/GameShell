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

        foreach (var expectedDisplay in expectedActive)
        {
            var actualDisplay = actualActive.SingleOrDefault(display => DisplayIdentityMatcher.IsReliableMatch(expectedDisplay.Identity, display.Identity));
            if (actualDisplay is null)
            {
                return false;
            }

            if (actualDisplay.PositionX != expectedDisplay.PositionX
                || actualDisplay.PositionY != expectedDisplay.PositionY
                || actualDisplay.Width != expectedDisplay.Width
                || actualDisplay.Height != expectedDisplay.Height)
            {
                return false;
            }
        }

        return true;
    }
}
