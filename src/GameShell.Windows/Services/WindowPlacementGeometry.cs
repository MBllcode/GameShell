namespace GameShell.Windows.Services;

/// <summary>
/// A rectangle in the Win32 virtual-desktop pixel coordinate space.
/// </summary>
public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

/// <summary>
/// A monitor work area in the Win32 virtual-desktop pixel coordinate space.
/// </summary>
public readonly record struct PixelWorkArea(
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
    private const double MaxLegacyScaledWidth = 3200;
    private const double MaxLegacyScaledHeight = 2200;

    public static bool IsMeaningfullyVisible(
        PixelRect window,
        IReadOnlyList<PixelWorkArea> monitors)
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

    public static PixelWorkArea? SelectRecoveryMonitor(
        IReadOnlyList<PixelWorkArea> monitors,
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

    public static PixelRect CenterAndClamp(
        PixelRect window,
        PixelWorkArea monitor)
    {
        var width = Math.Min(Math.Max(1, window.Width), Math.Max(1, monitor.Width));
        var height = Math.Min(Math.Max(1, window.Height), Math.Max(1, monitor.Height));
        var left = monitor.Left + Math.Max(0, (monitor.Width - width) / 2);
        var top = monitor.Top + Math.Max(0, (monitor.Height - height) / 2);
        return new PixelRect(left, top, width, height);
    }

    public static PixelRect? SelectValidationRect(
        PixelRect? currentRect,
        PixelRect? normalRestoreRect,
        bool isMinimized)
    {
        if (isMinimized && normalRestoreRect is not null)
        {
            return normalRestoreRect;
        }

        return currentRect ?? normalRestoreRect;
    }

    public static bool IsClearlyCorruptSavedBounds(
        DipRect saved,
        DipRect defaults)
    {
        if (!double.IsFinite(saved.Left) || !double.IsFinite(saved.Top)
            || !double.IsFinite(saved.Width) || !double.IsFinite(saved.Height)
            || saved.Width <= 0 || saved.Height <= 0)
        {
            return true;
        }

        // A previous regression persisted native bounds as WPF DIPs. These
        // thresholds deliberately target the clearly oversized 240%/300%
        // legacy values while preserving normal custom dashboard sizes.
        var widthThreshold = Math.Max(MaxLegacyScaledWidth, defaults.Width * 2.5);
        var heightThreshold = Math.Max(MaxLegacyScaledHeight, defaults.Height * 2.5);
        return saved.Width > widthThreshold || saved.Height > heightThreshold;
    }
}

/// <summary>
/// WPF logical coordinates. Persisted MainWindow bounds are stored in this
/// coordinate space, never as native pixels.
/// </summary>
public readonly record struct DipRect(double Left, double Top, double Width, double Height);

public readonly record struct DpiScaleFactor(double X, double Y)
{
    public static DpiScaleFactor Identity { get; } = new(1, 1);
}

public static class DpiCoordinateConverter
{
    public static PixelRect ToPixels(DipRect dips, DpiScaleFactor scale)
    {
        ValidateScale(scale);
        return new PixelRect(
            ConvertPositionToPixels(dips.Left, scale.X),
            ConvertPositionToPixels(dips.Top, scale.Y),
            ConvertSizeToPixels(dips.Width, scale.X),
            ConvertSizeToPixels(dips.Height, scale.Y));
    }

    public static DipRect ToDips(PixelRect pixels, DpiScaleFactor scale)
    {
        ValidateScale(scale);
        return new DipRect(
            pixels.Left / scale.X,
            pixels.Top / scale.Y,
            pixels.Width / scale.X,
            pixels.Height / scale.Y);
    }

    private static int ConvertPositionToPixels(double value, double scale)
        => checked((int)Math.Round(value * scale, MidpointRounding.AwayFromZero));

    private static int ConvertSizeToPixels(double value, double scale)
        => Math.Max(1, ConvertPositionToPixels(value, scale));

    private static void ValidateScale(DpiScaleFactor scale)
    {
        if (!double.IsFinite(scale.X) || !double.IsFinite(scale.Y)
            || scale.X <= 0 || scale.Y <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "DPI scale must be positive and finite.");
        }
    }
}
