using GameShell.Core.Displays;
using GameShell.Core.Settings;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameShell.Windows.Desktop;

public sealed partial class WindowsWindowManagementService
{
    internal static async Task OpenProfileApplicationAsync(
        ProfileApplication application,
        DisplayPathSnapshot display,
        bool hideTaskbar,
        SessionApplicationProcesses sessionApplications,
        CancellationToken cancellationToken)
    {
        var workArea = FindApplicationArea(display, hideTaskbar);
        if (workArea is null)
        {
            throw new InvalidOperationException("Windows could not locate the assigned display.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var isExecutable = string.Equals(Path.GetExtension(application.ExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase);
        var window = isExecutable ? FindApplicationWindow(application.ExecutablePath) : IntPtr.Zero;
        var requestedAtUtc = DateTime.UtcNow;
        using var started = window == IntPtr.Zero ? Process.Start(new ProcessStartInfo
        {
            FileName = application.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(application.ExecutablePath)!,
            UseShellExecute = true,
            WindowStyle = application.RunMinimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal
        }) : null;

        if (isExecutable && started is not null)
        {
            sessionApplications.Track(started, application.ExecutablePath, requestedAtUtc);
        }

        await StabilizeApplicationWindowAsync(
            () => isExecutable ? FindApplicationWindow(application.ExecutablePath) : FindDocumentWindow(application.ExecutablePath),
            candidate => IsOnAssignedDisplay(candidate, display, application.RunMinimized),
            candidate => PlaceProfileApplicationWindow(candidate, application, display, hideTaskbar),
            cancellationToken);
    }

    internal static async Task StabilizeApplicationWindowAsync(
        Func<IntPtr> findWindow, Func<IntPtr, bool> correctlyPlaced, Action<IntPtr> place,
        CancellationToken cancellationToken, Func<Task>? wait = null)
    {
        wait ??= () => Task.Delay(500, cancellationToken);
        var lastWindow = IntPtr.Zero;
        var stableSamples = 0;
        for (var attempt = 0; attempt < 120; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = findWindow();
            if (candidate == IntPtr.Zero) { stableSamples = 0; lastWindow = IntPtr.Zero; }
            else if (candidate != lastWindow || !correctlyPlaced(candidate))
            {
                place(candidate);
                lastWindow = candidate;
                stableSamples = 0;
            }
            else stableSamples++;
            // Observe startup for at least eight seconds, and require three seconds
            // without a replacement HWND or an application restoring its old position.
            if (attempt >= 16 && stableSamples >= 6) return;
            await wait();
        }
        throw new InvalidOperationException("The application's final window did not settle on its assigned display.");
    }

    private static bool IsOnAssignedDisplay(IntPtr window, DisplayPathSnapshot display, bool minimized)
    {
        if (minimized) return IsIconicProfileWindow(window);
        if (!GetWindowRect(window, out var bounds) || IsIconicProfileWindow(window)) return false;
        var monitor = MonitorFromProfileWindow(window, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(monitor, ref info)
            && info.MonitorArea.Left == display.PositionX && info.MonitorArea.Top == display.PositionY
            && bounds.Width >= info.WorkArea.Width - 16 && bounds.Height >= info.WorkArea.Height - 16;
    }

    [DllImport("user32.dll", EntryPoint = "MonitorFromWindow")]
    private static extern IntPtr MonitorFromProfileWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", EntryPoint = "IsIconic")]
    private static extern bool IsIconicProfileWindow(IntPtr window);

    private static void PlaceProfileApplicationWindow(IntPtr window, ProfileApplication application, DisplayPathSnapshot display, bool hideTaskbar)
    {
        var workArea = FindApplicationArea(display, hideTaskbar)
            ?? throw new InvalidOperationException("The assigned display is unavailable.");
        ShowWindow(window, application.RunMinimized ? 4 : 9); // SW_SHOWNOACTIVATE / SW_RESTORE
        if (!GetWindowRect(window, out var bounds))
        {
            throw new InvalidOperationException("The application window is unavailable.");
        }

        var borderless = IsBorderlessApplicationWindow(GetApplicationWindowStyle(window, -16));
        var area = borderless && !application.RunMinimized
            ? FindApplicationArea(display, true) ?? workArea
            : workArea;
        var width = Math.Clamp(bounds.Width, 1, area.Width);
        var height = Math.Clamp(bounds.Height, 1, area.Height);
        if (!SetWindowPos(window, IntPtr.Zero, area.Left + (area.Width - width) / 2,
            area.Top + (area.Height - height) / 2, width, height, SwpNoZOrder | SwpNoActivate))
        {
            throw new InvalidOperationException("Windows could not move the application window.");
        }

        if (application.RunMinimized)
        {
            ShowWindow(window, 7); // SW_SHOWMINNOACTIVE
        }
        else
        {
            SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, SwpNoSize | 0x0002 | SwpNoActivate);
            // Maximizing a borderless fullscreen game constrains its render surface to
            // rcWork (e.g. 3440x1380), even though its monitor is 3440x1440.
            // Keep it restored and size it directly to rcMonitor instead.
            if (!borderless) ShowWindow(window, 3);
            if (hideTaskbar || borderless)
            {
                // Hiding Shell_TrayWnd does not release the shell's reserved work area.
                // SW_MAXIMIZE alone therefore leaves a taskbar-sized gap. Explicitly
                // expand the maximized window to the physical monitor bounds.
                if (!SetWindowPos(window, IntPtr.Zero, area.Left, area.Top, area.Width, area.Height,
                    SwpNoZOrder | SwpNoActivate | 0x0020)) // SWP_FRAMECHANGED
                {
                    throw new InvalidOperationException("Windows could not expand the application to the full display.");
                }
            }
            SetForegroundWindow(window);
        }
    }

    private static IntPtr FindDocumentWindow(string path)
    {
        // Shell activation may reuse an existing editor and return no process.
        // Only accept a uniquely matching document title; never move an arbitrary foreground window.
        var matches = new List<IntPtr>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id != Environment.ProcessId && process.MainWindowHandle != IntPtr.Zero
                        && (GetApplicationWindowStyle(process.MainWindowHandle, -16) & 0x00040000) != 0
                        && process.MainWindowTitle.Contains(
                        Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase)) matches.Add(process.MainWindowHandle);
                }
                catch (InvalidOperationException) { }
            }
        }
        return matches.Count == 1 ? matches[0] : IntPtr.Zero;
    }

    internal static bool IsBorderlessApplicationWindow(int style) => (style & 0x00C00000) == 0; // WS_CAPTION

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetApplicationWindowStyle(IntPtr window, int index);

    private static IntPtr FindApplicationWindow(string executablePath)
    {
        var window = IntPtr.Zero;
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executablePath)))
        {
            using (process)
            {
                try
                {
                    if (window == IntPtr.Zero && process.MainWindowHandle != IntPtr.Zero
                        && string.Equals(process.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        window = process.MainWindowHandle;
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // The process may have exited or may belong to another session.
                }
            }
        }

        return window;
    }

    private static Rect? FindApplicationArea(DisplayPathSnapshot display, bool hideTaskbar)
    {
        Rect? result = null;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)
                || info.MonitorArea.Left != display.PositionX || info.MonitorArea.Top != display.PositionY)
            {
                return true;
            }

            result = hideTaskbar ? info.MonitorArea : info.WorkArea;
            return false;
        }, IntPtr.Zero);
        return result;
    }
}
