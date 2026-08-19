using System.Diagnostics;
using System.Runtime.InteropServices;
using GameShell.Core.Models;

namespace GameShell.Windows.Services;

public sealed class WindowsWindowManagementService : IWindowsWindowManagementService
{
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int SwMinimize = 6;
    private const int SwRestore = 9;
    private const uint MonitorInfoPrimary = 1;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const int WmClose = 0x0010;
    private const int ClassNameBufferSize = 256;

    public bool SetDesktopIconsVisible(bool visible)
    {
        var shellView = FindDesktopShellView();
        if (shellView == IntPtr.Zero)
        {
            return false;
        }

        var icons = FindWindowEx(shellView, IntPtr.Zero, "SysListView32", "FolderView");
        return icons != IntPtr.Zero && ShowWindow(icons, visible ? SwShow : SwHide);
    }

    public bool SetTaskbarsVisible(bool visible)
    {
        var command = visible ? SwShow : SwHide;
        var taskbars = new HashSet<IntPtr>();

        var mainTaskbar = FindWindow("Shell_TrayWnd", null);
        if (mainTaskbar != IntPtr.Zero)
        {
            taskbars.Add(mainTaskbar);
        }

        EnumWindows((window, _) =>
        {
            AddTaskbarWindow(window, taskbars);
            EnumChildWindows(window, (child, _) =>
            {
                AddTaskbarWindow(child, taskbars);
                return true;
            }, IntPtr.Zero);

            return true;
        }, IntPtr.Zero);

        var changed = false;
        foreach (var taskbar in taskbars)
        {
            changed |= ShowWindow(taskbar, command);
        }

        return changed;
    }

    private static void AddTaskbarWindow(IntPtr window, ISet<IntPtr> taskbars)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        var className = GetWindowClassName(window);
        if (string.Equals(className, "Shell_TrayWnd", StringComparison.Ordinal)
            || string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.Ordinal))
        {
            taskbars.Add(window);
        }
    }

    public void CloseVisibleWindows(GameShellSettings behaviour)
    {
        var closeOnly = CreateNameSet(behaviour.CloseOnlyProcessNames);
        foreach (var process in EnumerateCandidateWindowProcesses(behaviour))
        {
            if (closeOnly.Count > 0 && !closeOnly.Contains(process.ProcessName))
            {
                process.Dispose();
                continue;
            }

            try
            {
                if (!process.CloseMainWindow())
                {
                    PostMessage(process.MainWindowHandle, WmClose, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch
            {
                // Best-effort window cleanup.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    public void MinimizeRemainingWindows(GameShellSettings behaviour)
    {
        var protectedNames = CreateNameSet(behaviour.ProtectedProcessNames);
        protectedNames.Add(Process.GetCurrentProcess().ProcessName);

        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window))
            {
                return true;
            }

            try
            {
                GetWindowThreadProcessId(window, out var processId);
                if (processId == 0)
                {
                    return true;
                }

                using var process = Process.GetProcessById(processId);
                if (!protectedNames.Contains(process.ProcessName))
                {
                    ShowWindow(window, SwMinimize);
                }
            }
            catch
            {
                // Best-effort window cleanup.
            }

            return true;
        }, IntPtr.Zero);
    }

    public void MinimizeCurrentWindow()
    {
        try
        {
            var currentProcessId = Process.GetCurrentProcess().Id;
            var foreground = GetForegroundWindow();
            if (foreground != IntPtr.Zero)
            {
                _ = GetWindowThreadProcessId(foreground, out var foregroundProcessId);
                if (foregroundProcessId == currentProcessId)
                {
                    ShowWindow(foreground, SwMinimize);
                }
            }

            EnumWindows((window, _) =>
            {
                if (!IsWindowVisible(window))
                {
                    return true;
                }

                GetWindowThreadProcessId(window, out var processId);
                if (processId == currentProcessId)
                {
                    ShowWindow(window, SwMinimize);
                }

                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // Best-effort self minimize.
        }
    }

    public void CloseExplorerWindows(GameShellSettings behaviour)
    {
        var closeOnly = CreateNameSet(behaviour.CloseOnlyProcessNames);
        if (closeOnly.Count > 0 && !closeOnly.Contains("explorer"))
        {
            return;
        }

        EnumWindows((window, _) =>
        {
            if (IsWindowVisible(window) && IsExplorerFolderWindow(window))
            {
                PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
            }

            return true;
        }, IntPtr.Zero);
    }

    public void MinimizeExplorerWindows()
    {
        EnumWindows((window, _) =>
        {
            if (IsWindowVisible(window) && IsExplorerFolderWindow(window))
            {
                ShowWindow(window, SwMinimize);
            }

            return true;
        }, IntPtr.Zero);
    }

    public void RestoreAndFocus(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(windowHandle, SwRestore);
        SetForegroundWindow(windowHandle);
    }

    public bool MoveWindowToPrimaryDisplay(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        // UWP windows can still report their minimized bounds while the shell
        // activation is settling. Restore before calculating the target position.
        ShowWindow(windowHandle, SwRestore);
        if (!GetWindowRect(windowHandle, out var windowRect))
        {
            return false;
        }

        var primaryWorkArea = FindPrimaryWorkArea();
        if (primaryWorkArea is null)
        {
            return false;
        }

        var width = Math.Max(0, windowRect.Right - windowRect.Left);
        var height = Math.Max(0, windowRect.Bottom - windowRect.Top);
        var x = primaryWorkArea.Value.Left + Math.Max(0, (primaryWorkArea.Value.Width - width) / 2);
        var y = primaryWorkArea.Value.Top + Math.Max(0, (primaryWorkArea.Value.Height - height) / 2);

        return SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            x,
            y,
            0,
            0,
            SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    private static Rect? FindPrimaryWorkArea()
    {
        Rect? primaryWorkArea = null;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info)
                && (info.Flags & MonitorInfoPrimary) != 0)
            {
                primaryWorkArea = info.WorkArea;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return primaryWorkArea;
    }

    private static IntPtr FindDesktopShellView()
    {
        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            var shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                return shellView;
            }
        }

        var result = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            var shellView = FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView == IntPtr.Zero)
            {
                return true;
            }

            result = shellView;
            return false;
        }, IntPtr.Zero);

        return result;
    }

    private static bool IsExplorerFolderWindow(IntPtr window)
    {
        var className = GetWindowClassName(window);
        return string.Equals(className, "CabinetWClass", StringComparison.Ordinal)
            || string.Equals(className, "ExploreWClass", StringComparison.Ordinal);
    }

    private static string GetWindowClassName(IntPtr window)
    {
        var buffer = new string('\0', ClassNameBufferSize);
        var length = GetClassName(window, buffer, buffer.Length);
        return length > 0 ? buffer[..length] : string.Empty;
    }

    private static int GetClassName(IntPtr window, string className, int maxCount)
    {
        try
        {
            return GetClassNameNative(window, className, maxCount);
        }
        catch
        {
            return 0;
        }
    }

    private static IEnumerable<Process> EnumerateCandidateWindowProcesses(GameShellSettings behaviour)
    {
        var protectedNames = CreateNameSet(behaviour.ProtectedProcessNames);
        protectedNames.Add(Process.GetCurrentProcess().ProcessName);

        return Process.GetProcesses()
            .Where(process => process.MainWindowHandle != IntPtr.Zero)
            .Where(process => !protectedNames.Contains(process.ProcessName));
    }

    private static HashSet<string> CreateNameSet(IEnumerable<string>? names)
    {
        return names?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr clipRect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect MonitorArea;
        public Rect WorkArea;
        public uint Flags;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out int processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr windowHandle, out Rect rectangle);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr clipRect,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassNameNative(IntPtr windowHandle, string className, int maxCount);
}
