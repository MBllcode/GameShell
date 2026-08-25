namespace GameShell.Windows.Services;

public readonly record struct WindowPlacementRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

public readonly record struct MonitorWorkArea(
    string DeviceName,
    int Left,
    int Top,
    int Width,
    int Height,
    bool IsPrimary)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

public static class WindowPlacementCalculator
{
    private const int MeaningfulHeaderWidth = 240;
    private const int MeaningfulHeaderHeight = 80;

    public static bool IsMeaningfullyVisible(
        WindowPlacementRect window,
        IReadOnlyList<MonitorWorkArea> monitors)
    {
        if (window.Width <= 0 || window.Height <= 0)
        {
            return false;
        }

        var requiredWidth = Math.Min(MeaningfulHeaderWidth, window.Width);
        var requiredHeight = Math.Min(MeaningfulHeaderHeight, window.Height);
        return monitors.Any(monitor =>
        {
            var intersectionWidth = Math.Min(window.Right, monitor.Right)
                - Math.Max(window.Left, monitor.Left);
            var intersectionHeight = Math.Min(window.Bottom, monitor.Bottom)
                - Math.Max(window.Top, monitor.Top);
            return intersectionWidth >= requiredWidth
                && intersectionHeight >= requiredHeight;
        });
    }

    public static MonitorWorkArea? SelectRecoveryMonitor(
        IReadOnlyList<MonitorWorkArea> monitors,
        string? configuredWorkPrimaryDeviceName)
    {
        var primary = monitors.FirstOrDefault(monitor => monitor.IsPrimary);
        if (!string.IsNullOrWhiteSpace(primary.DeviceName))
        {
            return primary;
        }

        var configured = monitors.FirstOrDefault(monitor =>
            !string.IsNullOrWhiteSpace(configuredWorkPrimaryDeviceName)
            && string.Equals(
                monitor.DeviceName,
                configuredWorkPrimaryDeviceName,
                StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(configured.DeviceName))
        {
            return configured;
        }

        return monitors.FirstOrDefault();
    }

    public static WindowPlacementRect CenterAndClamp(
        WindowPlacementRect window,
        MonitorWorkArea monitor)
    {
        var width = Math.Min(Math.Max(1, window.Width), Math.Max(1, monitor.Width));
        var height = Math.Min(Math.Max(1, window.Height), Math.Max(1, monitor.Height));
        var left = monitor.Left + Math.Max(0, (monitor.Width - width) / 2);
        var top = monitor.Top + Math.Max(0, (monitor.Height - height) / 2);
        return new WindowPlacementRect(left, top, width, height);
    }
}
