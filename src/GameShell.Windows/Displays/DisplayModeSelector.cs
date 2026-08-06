namespace GameShell.Windows.Displays;

internal static class DisplayModeSelector
{
    public static DevMode? SelectHighestRefreshMode(IEnumerable<DevMode> modes, uint width, uint height)
    {
        var matches = modes
            .Where(mode => mode.PelsWidth == width && mode.PelsHeight == height && mode.DisplayFrequency > 0)
            .OrderByDescending(mode => mode.DisplayFrequency)
            .ThenByDescending(mode => mode.BitsPerPel)
            .ToList();

        return matches.Count == 0 ? null : matches[0];
    }
}
