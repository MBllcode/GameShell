using GameShell.Core.Settings;
using GameShell.Windows.Desktop;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.Windowing;

public sealed class WindowsWindowPlacementService : IWindowPlacementService
{
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint MonitorInfoPrimary = 0x00000001;
    private const int ShowMinimized = 2;
    private const int ShowMinimize = 6;
    private const int ShowMinNoActivate = 7;

    private readonly ISettingsService _settingsService;
    private readonly ILogger<WindowsWindowPlacementService> _logger;

    public WindowsWindowPlacementService(
        ISettingsService settingsService,
        ILogger<WindowsWindowPlacementService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<WindowPlacementResult> PrepareForPresentationAsync(
        Window window,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        var savedBoundsApplied = ApplySavedBounds(window, settings.Ui);

        if (!savedBoundsApplied || settings.Ui.WindowLeft is null || settings.Ui.WindowTop is null)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (!savedBoundsApplied)
            {
                _logger.LogWarning(
                    "[WINDOW PLACEMENT] Window=MainWindow SavedDipRect={SavedDipRect} Result=CorruptSavedBounds Action=UseDefaultSize",
                    FormatSavedBounds(settings.Ui));
            }
            else
            {
                _logger.LogInformation(
                    "[WINDOW PLACEMENT] Window=MainWindow Result=DefaultPrimary Action=CenterOnShow");
            }
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
        }

        return ReconcileCore(window, settings);
    }

    public async Task<WindowPlacementResult> ReconcileAsync(
        Window window,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        return ReconcileCore(window, settings);
    }

    private WindowPlacementResult ReconcileCore(Window window, AppSettings settings)
    {
        try
        {
            var monitors = GetMonitorWorkAreas();
            var handle = new WindowInteropHelper(window).EnsureHandle();
            if (handle == IntPtr.Zero || monitors.Count == 0)
            {
                _logger.LogWarning(
                    "[WINDOW PLACEMENT] Window=MainWindow Result=Unavailable Action=RetryOnNextShow");
                return new WindowPlacementResult("Unavailable", Message: "Monitor or window bounds unavailable.");
            }

            var hasCurrentRect = GetWindowRect(handle, out var nativeRect);
            var hasPlacement = TryGetWindowPlacement(handle, out var placement);
            PixelRect? normalRestoreRect;
            if (hasPlacement)
            {
                normalRestoreRect = ToPixelRect(placement.NormalPosition);
            }
            else if (hasCurrentRect)
            {
                normalRestoreRect = ToPixelRect(nativeRect);
            }
            else
            {
                normalRestoreRect = null;
            }
            var isMinimized = window.WindowState == WindowState.Minimized
                || (hasPlacement && IsMinimizedShowCommand(placement.ShowCommand));
            PixelRect? validationRect = WindowPlacementCalculator.SelectValidationRect(
                hasCurrentRect ? ToPixelRect(nativeRect) : (PixelRect?)null,
                normalRestoreRect,
                isMinimized);

            if (validationRect is null)
            {
                _logger.LogWarning(
                    "[WINDOW PLACEMENT] Window=MainWindow Result=Unavailable Action=RetryOnNextShow");
                return new WindowPlacementResult("Unavailable", Message: "Window bounds unavailable.");
            }

            var dpi = GetDpiScale(window);
            PixelRect? nativeCurrent = hasCurrentRect ? ToPixelRect(nativeRect) : (PixelRect?)null;
            LogPlacement(
                window,
                dpi,
                validationRect.Value,
                nativeCurrent,
                normalRestoreRect,
                monitors,
                isMinimized,
                "Validate");

            if (WindowPlacementCalculator.IsMeaningfullyVisible(validationRect.Value, monitors))
            {
                _logger.LogInformation(
                    "[WINDOW PLACEMENT] Window=MainWindow WindowState={WindowState} Result=Valid Action=Preserve",
                    window.WindowState);
                return new WindowPlacementResult("Valid");
            }

            var configuredPrimary = settings.WorkMode.DefaultDisplayLayout?.Displays
                .FirstOrDefault(display => display.IsActive && display.IsPrimary)
                ?.SourceDeviceName;
            var recoveryMonitor = WindowPlacementCalculator.SelectRecoveryMonitor(monitors, configuredPrimary);
            if (recoveryMonitor is null)
            {
                _logger.LogWarning(
                    "[WINDOW PLACEMENT] Window=MainWindow Result=NoRecoveryMonitor Action=RetryOnNextShow");
                return new WindowPlacementResult("Unavailable", Message: "No active monitor work area found.");
            }

            var repaired = WindowPlacementCalculator.CenterAndClamp(validationRect.Value, recoveryMonitor.Value);
            var moved = isMinimized && hasPlacement
                ? SetNormalRestorePosition(handle, placement, repaired)
                : SetWindowPos(
                    handle,
                    IntPtr.Zero,
                    repaired.Left,
                    repaired.Top,
                    repaired.Width,
                    repaired.Height,
                    SwpNoZOrder | SwpNoActivate);
            if (!moved)
            {
                _logger.LogWarning(
                    "[WINDOW PLACEMENT] Window=MainWindow Rect={Rect} Result=RepositionFailed RecoveryMonitor={RecoveryMonitor}",
                    Format(repaired),
                    recoveryMonitor.Value.DeviceName);
                return new WindowPlacementResult(
                    "RepositionFailed",
                    RecoveryMonitor: recoveryMonitor.Value.DeviceName);
            }

            LogPlacement(
                window,
                GetDpiScale(window),
                repaired,
                repaired,
                repaired,
                monitors,
                false,
                "Reposition");
            _logger.LogInformation(
                "[WINDOW PLACEMENT] Action=Reposition NewNativePixelRect={NewRect} Result=Success",
                Format(repaired));
            return new WindowPlacementResult(
                "Repositioned",
                Repositioned: true,
                RecoveryMonitor: recoveryMonitor.Value.DeviceName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WINDOW PLACEMENT] Window=MainWindow Result=Failed Action=RetryOnNextShow");
            return new WindowPlacementResult("Failed", Message: ex.Message);
        }
    }

    private static bool ApplySavedBounds(Window window, UiSettings ui)
    {
        if (ui.WindowLeft is null && ui.WindowTop is null
            && ui.WindowWidth is null && ui.WindowHeight is null)
        {
            return true;
        }

        var saved = new DipRect(
            ui.WindowLeft ?? 0,
            ui.WindowTop ?? 0,
            ui.WindowWidth ?? window.Width,
            ui.WindowHeight ?? window.Height);
        var defaults = new DipRect(window.Left, window.Top, window.Width, window.Height);
        if (WindowPlacementCalculator.IsClearlyCorruptSavedBounds(saved, defaults))
        {
            return false;
        }

        if (ui.WindowWidth is { } width && double.IsFinite(width))
        {
            window.Width = Math.Max(window.MinWidth, width);
        }

        if (ui.WindowHeight is { } height && double.IsFinite(height))
        {
            window.Height = Math.Max(window.MinHeight, height);
        }

        if (ui.WindowLeft is { } left && double.IsFinite(left))
        {
            window.Left = left;
        }

        if (ui.WindowTop is { } top && double.IsFinite(top))
        {
            window.Top = top;
        }

        return true;
    }

    private static IReadOnlyList<PixelWorkArea> GetMonitorWorkAreas()
    {
        var monitors = new List<PixelWorkArea>();
        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (monitorHandle, _, _, _) =>
            {
                var info = new MonitorInfoEx { Size = (uint)Marshal.SizeOf<MonitorInfoEx>() };
                if (GetMonitorInfo(monitorHandle, ref info))
                {
                    monitors.Add(new PixelWorkArea(
                        info.DeviceName,
                        info.Work.Left,
                        info.Work.Top,
                        Math.Max(1, info.Work.Right - info.Work.Left),
                        Math.Max(1, info.Work.Bottom - info.Work.Top),
                        (info.Flags & MonitorInfoPrimary) != 0));
                }

                return true;
            },
            IntPtr.Zero);
        return monitors;
    }

    private void LogPlacement(
        Window window,
        DpiScaleFactor dpi,
        PixelRect validationRect,
        PixelRect? nativeCurrent,
        PixelRect? normalRestoreRect,
        IReadOnlyList<PixelWorkArea> monitors,
        bool isMinimized,
        string action)
    {
        var wpfDipRect = new DipRect(
            window.Left,
            window.Top,
            window.ActualWidth > 0 ? window.ActualWidth : window.Width,
            window.ActualHeight > 0 ? window.ActualHeight : window.Height);
        _logger.LogInformation(
            "[WINDOW PLACEMENT] Window=MainWindow WindowState={WindowState} DpiX={DpiX:F2} DpiY={DpiY:F2} ScaleX={ScaleX:F2} ScaleY={ScaleY:F2} WpfDipRect={WpfDipRect} NativePixelRect={NativePixelRect} NormalRestorePixelRect={NormalRestorePixelRect} ValidationPixelRect={ValidationPixelRect} MonitorPixelWorkArea={MonitorPixelWorkArea} Minimized={Minimized} Action={Action}",
            window.WindowState,
            dpi.X * 96,
            dpi.Y * 96,
            dpi.X,
            dpi.Y,
            Format(wpfDipRect),
            nativeCurrent is null ? "-" : Format(nativeCurrent.Value),
            normalRestoreRect is null ? "-" : Format(normalRestoreRect.Value),
            Format(validationRect),
            Format(monitors),
            isMinimized,
            action);
    }

    private static DpiScaleFactor GetDpiScale(Window window)
    {
        var source = PresentationSource.FromVisual(window);
        var transform = source?.CompositionTarget?.TransformToDevice;
        if (transform is Matrix matrix && matrix.M11 > 0 && matrix.M22 > 0)
        {
            return new DpiScaleFactor(matrix.M11, matrix.M22);
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        return new DpiScaleFactor(dpi.DpiScaleX, dpi.DpiScaleY);
    }

    private static bool TryGetWindowPlacement(IntPtr handle, out NativeWindowPlacement placement)
    {
        placement = new NativeWindowPlacement { Length = (uint)Marshal.SizeOf<NativeWindowPlacement>() };
        return GetWindowPlacement(handle, ref placement);
    }

    private static bool IsMinimizedShowCommand(int showCommand)
        => showCommand is ShowMinimized or ShowMinimize or ShowMinNoActivate;

    private static bool SetNormalRestorePosition(
        IntPtr handle,
        NativeWindowPlacement placement,
        PixelRect repaired)
    {
        placement.NormalPosition = ToNativeRect(repaired);
        placement.Length = (uint)Marshal.SizeOf<NativeWindowPlacement>();
        return SetWindowPlacement(handle, ref placement);
    }

    private static PixelRect ToPixelRect(NativeRect rect)
        => new(
            rect.Left,
            rect.Top,
            Math.Max(1, rect.Right - rect.Left),
            Math.Max(1, rect.Bottom - rect.Top));

    private static NativeRect ToNativeRect(PixelRect rect)
        => new()
        {
            Left = rect.Left,
            Top = rect.Top,
            Right = rect.Right,
            Bottom = rect.Bottom
        };

    private static string Format(PixelRect rect)
        => $"{rect.Left},{rect.Top},{rect.Width}x{rect.Height}";

    private static string Format(DipRect rect)
        => $"{rect.Left:F1},{rect.Top:F1},{rect.Width:F1}x{rect.Height:F1}";

    private static string FormatSavedBounds(UiSettings ui)
        => $"{ui.WindowLeft?.ToString("F1") ?? "-"},{ui.WindowTop?.ToString("F1") ?? "-"},{ui.WindowWidth?.ToString("F1") ?? "-"}x{ui.WindowHeight?.ToString("F1") ?? "-"}";

    private static string Format(IReadOnlyList<PixelWorkArea> monitors)
        => string.Join(
            ";",
            monitors.Select(monitor =>
                $"{monitor.DeviceName}:{monitor.Left},{monitor.Top},{monitor.Width}x{monitor.Height}"));

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeWindowPlacement
    {
        public uint Length;
        public uint Flags;
        public int ShowCommand;
        public NativePoint MinPosition;
        public NativePoint MaxPosition;
        public NativeRect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    private delegate bool MonitorEnumProc(
        IntPtr monitorHandle,
        IntPtr deviceContext,
        IntPtr monitorRect,
        IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr deviceContext,
        IntPtr clipRect,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(
        IntPtr monitorHandle,
        ref MonitorInfoEx monitorInfo);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(
        IntPtr windowHandle,
        ref NativeWindowPlacement placement);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(
        IntPtr windowHandle,
        ref NativeWindowPlacement placement);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
