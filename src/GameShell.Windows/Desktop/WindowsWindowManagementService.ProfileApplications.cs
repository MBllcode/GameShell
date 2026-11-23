using GameShell.Core.Displays;
using GameShell.Core.Settings;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Desktop;

public sealed partial class WindowsWindowManagementService
{
    internal static async Task<IntPtr> OpenProfileApplicationAsync(
        ProfileApplication application,
        DisplayPathSnapshot? display,
        bool hideTaskbar,
        SessionApplicationProcesses sessionApplications,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (application.RunMinimized && (display is null || FindApplicationArea(display, hideTaskbar) is null))
        {
            throw new InvalidOperationException("Windows could not locate the assigned display.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var isExecutable = string.Equals(Path.GetExtension(application.ExecutablePath), ".exe", StringComparison.OrdinalIgnoreCase);
        var alternateExecutable = application.RunMinimized
            ? PlayniteSessionApplication.GetAlternateExecutable(application.ExecutablePath)
            : null;
        var window = isExecutable ? FindWindowForExecutable() : IntPtr.Zero;
        var observeStartup = window == IntPtr.Zero;
        var existingProcessIds = isExecutable && window == IntPtr.Zero
            ? SessionApplicationProcesses.CaptureExistingProcesses(application.ExecutablePath)
            : null;
        using var started = window == IntPtr.Zero ? Process.Start(new ProcessStartInfo
        {
            FileName = application.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(application.ExecutablePath)!,
            UseShellExecute = true,
            Arguments = alternateExecutable is null ? string.Empty : "--hidesplashscreen",
            WindowStyle = application.RunMinimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal
        }) : null;

        if (existingProcessIds is not null && started is not null)
        {
            sessionApplications.Track(started, application.ExecutablePath, existingProcessIds);
        }

        var applicationName = Path.GetFileName(application.ExecutablePath);
        if (!application.RunMinimized)
        {
            if (window != IntPtr.Zero)
            {
                if (IsIconicProfileWindow(window)) ShowWindow(window, 9); // SW_RESTORE
                SetForegroundWindow(window);
            }
            logger.LogInformation("[PROFILE APPLICATION WINDOW] App={Application} Stage=DefaultWindow", applicationName);
            return IntPtr.Zero;
        }

        var assignedDisplay = display ?? throw new InvalidOperationException("The assigned display is unavailable.");
        var measuredWindows = new HashSet<IntPtr>();
        await StabilizeApplicationWindowAsync(
            () =>
            {
                var candidate = isExecutable ? FindWindowForExecutable() : FindDocumentWindow(application.ExecutablePath);
                if (existingProcessIds is not null && candidate != IntPtr.Zero && candidate != window)
                {
                    TrackLaunchedApplicationWindow(candidate, application.ExecutablePath, alternateExecutable, existingProcessIds, sessionApplications);
                }

                window = candidate;
                return candidate;
            },
            IsIconicProfileWindow,
            candidate =>
            {
                var firstPlacement = measuredWindows.Add(candidate);
                if (firstPlacement) LogProfileWindowGeometry(logger, applicationName, "BeforePlacement", candidate);
                PlaceMinimizedProfileApplicationWindow(candidate, assignedDisplay, hideTaskbar);
                if (firstPlacement) LogProfileWindowGeometry(logger, applicationName, "AfterPlacement", candidate);
            },
            cancellationToken,
            observeStartup: observeStartup && alternateExecutable is null);
        LogProfileWindowGeometry(logger, applicationName, "Settled", window);
        return window;

        IntPtr FindWindowForExecutable()
        {
            var primaryWindow = FindApplicationWindow(application.ExecutablePath);
            return primaryWindow != IntPtr.Zero || alternateExecutable is null
                ? primaryWindow
                : FindApplicationWindow(alternateExecutable);
        }
    }

    private static void TrackLaunchedApplicationWindow(
        IntPtr window, string executablePath, string? alternateExecutable,
        IReadOnlySet<int> existingProcessIds, SessionApplicationProcesses sessionApplications)
    {
        if (GetWindowThreadProcessId(window, out var processId) == 0) return;

        try
        {
            using var process = Process.GetProcessById(processId);
            var actualPath = process.MainModule?.FileName;
            if (actualPath is not null
                && (string.Equals(actualPath, executablePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actualPath, alternateExecutable, StringComparison.OrdinalIgnoreCase)))
            {
                sessionApplications.Track(process, actualPath, existingProcessIds, window);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // A splash window can disappear between enumeration and lookup.
        }
    }

    internal static bool RequestSessionApplicationClose(Process process, IntPtr window)
    {
        if (window != IntPtr.Zero && IsWindowVisible(window)
            && GetWindowThreadProcessId(window, out var processId) != 0 && processId == process.Id
            && PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero))
        {
            return true;
        }

        return process.CloseMainWindow();
    }

    internal static async Task StabilizeApplicationWindowAsync(
        Func<IntPtr> findWindow, Func<IntPtr, bool> correctlyPlaced, Action<IntPtr> place,
        CancellationToken cancellationToken, Func<Task>? wait = null, bool observeStartup = true)
    {
        const int maximumSamples = 51;
        const int startupSamples = 16;
        const int stableSamplesAfterLaunch = 6;
        const int stableSamplesForExistingWindow = 2;
        var requiredStableSamples = observeStartup ? stableSamplesAfterLaunch : stableSamplesForExistingWindow;
        wait ??= () => Task.Delay(500, cancellationToken);
        var lastWindow = IntPtr.Zero;
        var stableSamples = 0;
        var sawWindow = false;
        for (var sample = 0; sample < maximumSamples; sample++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = findWindow();
            sawWindow |= candidate != IntPtr.Zero;
            if (candidate == IntPtr.Zero) { stableSamples = 0; lastWindow = IntPtr.Zero; }
            else if (candidate != lastWindow || !correctlyPlaced(candidate))
            {
                place(candidate);
                lastWindow = candidate;
                stableSamples = 0;
            }
            else stableSamples++;
            if ((!observeStartup || sample >= startupSamples) && stableSamples >= requiredStableSamples) return;
            if (sample + 1 < maximumSamples) await wait();
        }
        throw new InvalidOperationException(sawWindow
            ? "The application's window did not settle on its assigned display within 25 seconds."
            : "No application window was found within 25 seconds.");
    }

    [DllImport("user32.dll", EntryPoint = "MonitorFromWindow")]
    private static extern IntPtr MonitorFromProfileWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", EntryPoint = "IsIconic")]
    private static extern bool IsIconicProfileWindow(IntPtr window);

    private static void PlaceMinimizedProfileApplicationWindow(IntPtr window, DisplayPathSnapshot display, bool hideTaskbar)
    {
        var area = FindApplicationArea(display, hideTaskbar)
            ?? throw new InvalidOperationException("The assigned display is unavailable.");
        ShowWindow(window, 4); // SW_SHOWNOACTIVATE
        if (!GetWindowRect(window, out var bounds))
        {
            throw new InvalidOperationException("The application window is unavailable.");
        }

        var width = Math.Clamp(bounds.Width, 1, area.Width);
        var height = Math.Clamp(bounds.Height, 1, area.Height);
        if (!SetWindowPos(window, IntPtr.Zero, area.Left + (area.Width - width) / 2,
            area.Top + (area.Height - height) / 2, width, height, SwpNoZOrder | SwpNoActivate))
        {
            throw new InvalidOperationException("Windows could not move the application window.");
        }

        ShowWindow(window, 7); // SW_SHOWMINNOACTIVE
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
