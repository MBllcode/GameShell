using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Windows.Services;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.Services;

public sealed class WindowsWindowPlacementService : IWindowPlacementService
{
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

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
        ApplySavedBounds(window, settings.Ui);

        if (settings.Ui.WindowLeft is null || settings.Ui.WindowTop is null)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _logger.LogInformation(
                "[WINDOW PLACEMENT] Window=MainWindow Result=DefaultPrimary Action=CenterOnShow");
            return new WindowPlacementResult("DefaultPrimary");
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
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
            if (handle == IntPtr.Zero || monitors.Count == 0 || !GetWindowRect(handle, out var nativeRect))
            {
                _logger.LogWarning(
                    "[WINDOW PLACEMENT] Window=MainWindow Result=Unavailable Action=RetryOnNextShow");
                return new WindowPlacementResult("Unavailable", Message: "Monitor or window bounds unavailable.");
            }

            var current = new WindowPlacementRect(
                nativeRect.Left,
                nativeRect.Top,
                Math.Max(1, nativeRect.Right - nativeRect.Left),
                Math.Max(1, nativeRect.Bottom - nativeRect.Top));
            if (WindowPlacementCalculator.IsMeaningfullyVisible(current, monitors))
            {
                _logger.LogInformation(
                    "[WINDOW PLACEMENT] Window=MainWindow Rect={Rect} ActiveWorkAreas={ActiveWorkAreas} Result=Valid",
                    Format(current),
                    Format(monitors));
                return new WindowPlacementResult("Valid");
            }

            var configuredPrimary = settings.WorkMode.DefaultDisplayLayout?.Displays
                .FirstOrDefault(display => display.IsActive && display.IsPrimary)
                ?.SourceDeviceName;
            var recoveryMonitor = WindowPlacementCalculator.SelectRecoveryMonitor(monitors, configuredPrimary);
            if (recoveryMonitor is null)
            {
                _logger.LogWarning(
                    "[WINDOW PLACEMENT] Window=MainWindow Rect={Rect} Result=NoRecoveryMonitor",
                    Format(current));
                return new WindowPlacementResult("Unavailable", Message: "No active monitor work area found.");
            }

            var repaired = WindowPlacementCalculator.CenterAndClamp(current, recoveryMonitor.Value);
            var moved = SetWindowPos(
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
                    Format(current),
                    recoveryMonitor.Value.DeviceName);
                return new WindowPlacementResult(
                    "RepositionFailed",
                    RecoveryMonitor: recoveryMonitor.Value.DeviceName);
            }

            _logger.LogInformation(
                "[WINDOW PLACEMENT] Window=MainWindow Rect={Rect} Result=OffScreen RecoveryMonitor={RecoveryMonitor} Action=Reposition",
                Format(current),
                recoveryMonitor.Value.DeviceName);
            _logger.LogInformation(
                "[WINDOW PLACEMENT] Action=Reposition NewRect={NewRect} Result=Success",
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

    private static void ApplySavedBounds(Window window, UiSettings ui)
    {
        if (ui.WindowWidth is not null)
        {
            window.Width = Math.Max(window.MinWidth, ui.WindowWidth.Value);
        }

        if (ui.WindowHeight is not null)
        {
            window.Height = Math.Max(window.MinHeight, ui.WindowHeight.Value);
        }

        if (ui.WindowLeft is not null)
        {
            window.Left = ui.WindowLeft.Value;
        }

        if (ui.WindowTop is not null)
        {
            window.Top = ui.WindowTop.Value;
        }
    }

    private static IReadOnlyList<MonitorWorkArea> GetMonitorWorkAreas()
    {
        return Screen.AllScreens
            .Select(screen => new MonitorWorkArea(
                screen.DeviceName,
                screen.WorkingArea.Left,
                screen.WorkingArea.Top,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height,
                screen.Primary))
            .ToList();
    }

    private static string Format(WindowPlacementRect rect)
        => $"{rect.Left},{rect.Top},{rect.Width}x{rect.Height}";

    private static string Format(IReadOnlyList<MonitorWorkArea> monitors)
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

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

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
