using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Desktop;

public sealed partial class WindowsWindowManagementService
{
    internal static async Task ObserveProfileWindowAfterSetupAsync(
        ILogger logger, string applicationName, IntPtr window, CancellationToken cancellationToken)
    {
        var threadId = GetWindowThreadProcessId(window, out var processId);
        if (threadId == 0) return;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            if (!LogSampleForOriginalWindow("AfterProfileSetup+1s")) return;
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            LogSampleForOriginalWindow("AfterProfileSetup+4s");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        bool LogSampleForOriginalWindow(string stage)
        {
            if (GetWindowThreadProcessId(window, out var currentProcessId) == threadId && currentProcessId == processId)
            {
                LogProfileWindowGeometry(logger, applicationName, stage, window);
                return true;
            }

            logger.LogInformation("[PROFILE APPLICATION WINDOW] App={Application} Stage={Stage} HWND=0x{Window:X} PID={ProcessId} Result=WindowGone",
                applicationName, stage, window.ToInt64(), processId);
            return false;
        }
    }

    private static void LogProfileWindowGeometry(ILogger logger, string applicationName, string stage, IntPtr window)
    {
        if (!logger.IsEnabled(LogLevel.Information)) return;

        const int extendedFrameBounds = 9; // DWMWA_EXTENDED_FRAME_BOUNDS
        GetWindowThreadProcessId(window, out var processId);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        var hasMonitor = GetMonitorInfo(MonitorFromProfileWindow(window, 2), ref info);
        logger.LogInformation(
            "[PROFILE APPLICATION WINDOW] App={Application} Stage={Stage} HWND=0x{Window:X} PID={ProcessId} Style=0x{Style:X8} Visible={Visible} Minimized={Minimized} Maximized={Maximized} Dpi={Dpi} WindowRect={WindowRect} VisibleFramePx={VisibleFrame} ClientSize={ClientSize} MonitorArea={MonitorArea} WorkArea={WorkArea}",
            applicationName, stage, window.ToInt64(), processId, GetApplicationWindowStyle(window, -16),
            IsWindowVisible(window), IsIconicProfileWindow(window), IsZoomed(window), GetDpiForWindow(window),
            GetWindowRect(window, out var bounds) ? Format(bounds) : "unavailable",
            DwmGetWindowAttribute(window, extendedFrameBounds, out var frame, Marshal.SizeOf<Rect>()) >= 0
                ? Format(frame) : "unavailable",
            GetClientRect(window, out var client) ? $"{client.Width}x{client.Height}" : "unavailable",
            hasMonitor ? Format(info.MonitorArea) : "unavailable",
            hasMonitor ? Format(info.WorkArea) : "unavailable");

        static string Format(Rect rect) => $"{rect.Left},{rect.Top},{rect.Width}x{rect.Height}";
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect rect, int size);
}
