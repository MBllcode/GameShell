using System.Diagnostics;
using System.Runtime.InteropServices;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Services;

public sealed class NativeModeWorkflowService : IModeWorkflowService
{
    private static readonly TimeSpan LauncherShutdownTimeout = TimeSpan.FromSeconds(2);
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const int SwMinimize = 6;
    private const int SwRestore = 9;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const int WmClose = 0x0010;
    private const int SpiSetCursors = 0x0057;
    private const int ClassNameBufferSize = 256;
    private const int CursorWidth = 32;
    private const int CursorHeight = 32;

    private static readonly int[] SystemCursorIds =
    [
        32512, // OCR_NORMAL
        32513, // OCR_IBEAM
        32514, // OCR_WAIT
        32515, // OCR_CROSS
        32516, // OCR_UP
        32642, // OCR_SIZE
        32643, // OCR_ICON
        32644, // OCR_SIZENWSE
        32645, // OCR_SIZENESW
        32646, // OCR_SIZEWE
        32648, // OCR_SIZENS
        32649, // OCR_SIZEALL
        32650, // OCR_NO
        32651, // OCR_HAND
        32671, // OCR_APPSTARTING
        32672 // OCR_HELP
    ];

    private readonly ILogger<NativeModeWorkflowService> _logger;
    private readonly IAudioDeviceService _audioDeviceService;
    private readonly IRuntimeStateService _runtimeStateService;

    public NativeModeWorkflowService(
        ILogger<NativeModeWorkflowService> logger,
        IAudioDeviceService audioDeviceService,
        IRuntimeStateService runtimeStateService)
    {
        _logger = logger;
        _audioDeviceService = audioDeviceService;
        _runtimeStateService = runtimeStateService;
    }

    public async Task<ModeWorkflowResult> PrepareGamingWorkflowAsync(
        AppSettings settings,
        GameShellMode mode,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var behaviour = mode == GameShellMode.PcGaming ? settings.PcGaming : settings.GameShell;

        await TryStepAsync("hide desktop icons", warnings, async () =>
        {
            if (behaviour.HideDesktopIcons)
            {
                await RetryAsync(() => SetDesktopIconsVisible(false), cancellationToken);
            }
        }, cancellationToken);

        await TryStepAsync("close visible windows", warnings, async () =>
        {
            if (behaviour.CloseVisibleWindows)
            {
                CloseVisibleWindows(settings, behaviour);
                CloseExplorerWindows(behaviour);
                await Task.Delay(700, cancellationToken);
            }
        }, cancellationToken);

        await TryStepAsync("minimize remaining windows", warnings, async () =>
        {
            if (behaviour.MinimizeRemainingWindows)
            {
                MinimizeRemainingWindows(settings, behaviour);
                MinimizeCurrentWindow();
                MinimizeExplorerWindows();
            }

            await Task.CompletedTask;
        }, cancellationToken);

        await TryStepAsync("hide taskbars", warnings, async () =>
        {
            if (behaviour.HideTaskbar)
            {
                await RetryAsync(() => SetTaskbarsVisible(false), cancellationToken);
            }
        }, cancellationToken);

        await TryStepAsync("move cursor", warnings, async () =>
        {
            if (behaviour.MoveCursorToCorner)
            {
                MoveCursorToBottomRight();
            }

            await Task.CompletedTask;
        }, cancellationToken);

        await TryStepAsync("hide cursor", warnings, async () =>
        {
            if (behaviour.HideCursor)
            {
                HideSystemCursor();
            }

            await Task.CompletedTask;
        }, cancellationToken);

        return warnings.Count == 0
            ? ModeWorkflowResult.Ok()
            : ModeWorkflowResult.Ok($"Workflow zakonczony z ostrzezeniami: {string.Join("; ", warnings)}");
    }

    public async Task<ModeWorkflowResult> CompleteGamingWorkflowAsync(
        AppSettings settings,
        GameShellMode mode,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var behaviour = mode == GameShellMode.PcGaming ? settings.PcGaming : settings.GameShell;
        Process? launcherProcess = null;

        await TryStepAsync("minimize remaining windows after display transition", warnings, async () =>
        {
            if (behaviour.MinimizeRemainingWindows)
            {
                MinimizeRemainingWindows(settings, behaviour);
                MinimizeCurrentWindow();
                MinimizeExplorerWindows();
            }

            await Task.CompletedTask;
        }, cancellationToken);

        await TryStepAsync("start launcher", warnings, async () =>
        {
            if (behaviour.StartLauncher)
            {
                launcherProcess = StartOrFindLauncher(settings, behaviour);
            }

            await Task.CompletedTask;
        }, cancellationToken);

        await TryStepAsync("bring launcher to foreground", warnings, async () =>
        {
            if (behaviour.StartLauncher && behaviour.BringLauncherToForeground)
            {
                await Task.Delay(1500, cancellationToken);
                BringLauncherToForeground(settings, launcherProcess);
            }
        }, cancellationToken);

        await TryStepAsync("move cursor", warnings, async () =>
        {
            if (behaviour.MoveCursorToCorner)
            {
                MoveCursorToBottomRight();
            }

            await Task.CompletedTask;
        }, cancellationToken);

        await TryStepAsync("hide cursor", warnings, async () =>
        {
            if (behaviour.HideCursor)
            {
                HideSystemCursor();
            }

            await Task.CompletedTask;
        }, cancellationToken);

        return warnings.Count == 0
            ? ModeWorkflowResult.Ok()
            : ModeWorkflowResult.Ok($"Workflow zakonczony z ostrzezeniami: {string.Join("; ", warnings)}");
    }

    public async Task<ModeWorkflowResult> RunGamingWorkflowAsync(
        AppSettings settings,
        GameShellMode mode,
        CancellationToken cancellationToken = default)
    {
        var pre = await PrepareGamingWorkflowAsync(settings, mode, cancellationToken);
        if (!pre.Success)
        {
            return pre;
        }

        return await CompleteGamingWorkflowAsync(settings, mode, cancellationToken);
    }

    public async Task<ModeWorkflowResult> RunWorkWorkflowAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();

        await TryStepAsync("restore cursor", warnings, async () =>
        {
            RestoreSystemCursors();
            await Task.CompletedTask;
        }, cancellationToken);

        await TryStepAsync("show taskbars", warnings, async () =>
        {
            if (settings.WorkMode.ShowTaskbars)
            {
                await RetryAsync(() => SetTaskbarsVisible(true), cancellationToken);
            }
        }, cancellationToken);

        await TryStepAsync("show desktop icons", warnings, async () =>
        {
            if (settings.WorkMode.ShowDesktopIcons)
            {
                await RestoreDesktopIconsAsync(settings.WorkMode.DesktopIconsDelaySeconds, cancellationToken);
            }
        }, cancellationToken);

        return warnings.Count == 0
            ? ModeWorkflowResult.Ok()
            : ModeWorkflowResult.Ok($"Workflow Work Mode zakonczony z ostrzezeniami: {string.Join("; ", warnings)}");
    }

    public async Task<ModeWorkflowResult> PrepareWorkWorkflowAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();

        await TryStepAsync("close launcher", warnings, async () =>
        {
            if (settings.WorkMode.CloseLauncher)
            {
                await CloseLauncherAsync(settings, cancellationToken);
            }
        }, cancellationToken);

        return warnings.Count == 0
            ? ModeWorkflowResult.Ok()
            : ModeWorkflowResult.Ok($"Przygotowanie Work Mode zakonczone z ostrzezeniami: {string.Join("; ", warnings)}");
    }

    private async Task TryStepAsync(
        string name,
        List<string> warnings,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transitionLabel = WorkModeTransitionContext.CurrentId is null ? "MODE TRANSITION" : "WORK MODE TRANSITION";
        _logger.LogInformation("{TransitionLabel} [{TransitionId}] STEP START {StepName}.", transitionLabel, WorkModeTransitionContext.CurrentId ?? "-", name);
        try
        {
            await action();
            _logger.LogInformation("{TransitionLabel} [{TransitionId}] STEP SUCCESS {StepName}.", transitionLabel, WorkModeTransitionContext.CurrentId ?? "-", name);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            warnings.Add(name);
            _logger.LogWarning(ex, "{TransitionLabel} [{TransitionId}] STEP FAILED {StepName}.", transitionLabel, WorkModeTransitionContext.CurrentId ?? "-", name);
        }
    }

    private async Task RestoreDesktopIconsAsync(int delaySeconds, CancellationToken cancellationToken)
    {
        try
        {
            if (SetDesktopIconsVisible(true))
            {
                return;
            }

            await RetryAsync(() => SetDesktopIconsVisible(true), cancellationToken);
            var fallbackDelaySeconds = Math.Clamp(delaySeconds, 0, 5);
            if (fallbackDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(fallbackDelaySeconds), cancellationToken);
                await RetryAsync(() => SetDesktopIconsVisible(true), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WORK MODE TRANSITION [{TransitionId}] desktop icon restore failed.", WorkModeTransitionContext.CurrentId ?? "-");
            throw;
        }
    }

    private static async Task RetryAsync(Func<bool> action, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (action())
            {
                return;
            }

            await Task.Delay(400, cancellationToken);
        }
    }

    private static bool SetDesktopIconsVisible(bool visible)
    {
        var shellView = FindDesktopShellView();
        if (shellView == IntPtr.Zero)
        {
            return false;
        }

        var icons = FindWindowEx(shellView, IntPtr.Zero, "SysListView32", "FolderView");
        if (icons == IntPtr.Zero)
        {
            return false;
        }

        return ShowWindow(icons, visible ? SwShow : SwHide);
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

    private static bool SetTaskbarsVisible(bool visible)
    {
        var command = visible ? SwShow : SwHide;
        var changed = false;

        var mainTaskbar = FindWindow("Shell_TrayWnd", null);
        if (mainTaskbar != IntPtr.Zero)
        {
            changed |= ShowWindow(mainTaskbar, command);
        }

        var secondaryTaskbar = IntPtr.Zero;
        do
        {
            secondaryTaskbar = FindWindowEx(IntPtr.Zero, secondaryTaskbar, "Shell_SecondaryTrayWnd", null);
            if (secondaryTaskbar != IntPtr.Zero)
            {
                changed |= ShowWindow(secondaryTaskbar, command);
            }
        }
        while (secondaryTaskbar != IntPtr.Zero);

        return changed;
    }

    private static void CloseVisibleWindows(AppSettings settings, GameShellSettings behaviour)
    {
        var closeOnly = CreateNameSet(behaviour.CloseOnlyProcessNames);
        foreach (var process in EnumerateCandidateWindowProcesses(settings, behaviour))
        {
            if (closeOnly.Count > 0 && !closeOnly.Contains(process.ProcessName))
            {
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
        }
    }

    private static void MinimizeRemainingWindows(AppSettings settings, GameShellSettings behaviour)
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
                if (protectedNames.Contains(process.ProcessName))
                {
                    return true;
                }

                ShowWindow(window, SwMinimize);
            }
            catch
            {
                // Best-effort window cleanup.
            }

            return true;
        }, IntPtr.Zero);
    }

    private static void MinimizeCurrentWindow()
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

            EnumWindows((window, lParam) =>
            {
                if (!IsWindowVisible(window))
                {
                    return true;
                }

                GetWindowThreadProcessId(window, out var processId);
                if (processId != currentProcessId)
                {
                    return true;
                }

                ShowWindow(window, SwMinimize);
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // Best-effort self minimize.
        }
    }

    private static void CloseExplorerWindows(GameShellSettings behaviour)
    {
        var closeOnly = CreateNameSet(behaviour.CloseOnlyProcessNames);
        if (closeOnly.Count > 0 && !closeOnly.Contains("explorer"))
        {
            return;
        }

        EnumWindows((window, lParam) =>
        {
            if (!IsWindowVisible(window))
            {
                return true;
            }

            if (!IsExplorerFolderWindow(window))
            {
                return true;
            }

            PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
            return true;
        }, IntPtr.Zero);
    }

    private static void MinimizeExplorerWindows()
    {
        EnumWindows((window, lParam) =>
        {
            if (!IsWindowVisible(window))
            {
                return true;
            }

            if (!IsExplorerFolderWindow(window))
            {
                return true;
            }

            ShowWindow(window, SwMinimize);
            return true;
        }, IntPtr.Zero);
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
        if (length <= 0)
        {
            return string.Empty;
        }

        return buffer[..length];
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

    private static IEnumerable<Process> EnumerateCandidateWindowProcesses(AppSettings settings, GameShellSettings behaviour)
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

    private static Process? StartOrFindLauncher(AppSettings settings, GameShellSettings behaviour)
    {
        var existing = FindLauncherProcess(settings);
        if (existing is not null)
        {
            return existing;
        }

        if (settings.Launcher.Type.Equals("Steam", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(settings.Launcher.ExecutablePath)
                    ? "steam://open/bigpicture"
                    : settings.Launcher.ExecutablePath,
                UseShellExecute = true
            });
            return null;
        }

        if (settings.Launcher.Type.Equals("XboxApp", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "shell:AppsFolder\\Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App",
                UseShellExecute = true
            });
            return null;
        }

        var executablePath = PathExpander.Expand(settings.Launcher.ExecutablePath);
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        return Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = settings.Launcher.Arguments,
            UseShellExecute = false
        });
    }

    private static void BringLauncherToForeground(AppSettings settings, Process? launcherProcess)
    {
        var process = launcherProcess;
        if (process is null || process.HasExited || process.MainWindowHandle == IntPtr.Zero)
        {
            process = FindLauncherProcess(settings);
        }

        if (process is null || process.MainWindowHandle == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(process.MainWindowHandle, SwRestore);
        SetForegroundWindow(process.MainWindowHandle);
    }

    private static Process? FindLauncherProcess(AppSettings settings)
    {
        var processNames = GetLauncherProcessNames(settings).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (processNames.Count == 0)
        {
            return null;
        }

        return Process.GetProcesses()
            .Where(process => process.MainWindowHandle != IntPtr.Zero)
            .FirstOrDefault(process => processNames.Contains(process.ProcessName));
    }

    private static IEnumerable<string> GetLauncherProcessNames(AppSettings settings)
    {
        // Always include known launcher processes so Work Mode can reliably close
        // the launcher used by the active gaming profile even if global launcher differs.
        yield return "Playnite.FullscreenApp";
        yield return "Playnite.DesktopApp";
        yield return "steam";
        yield return "Xbox";
        yield return "XboxPcApp";
        yield return "GamingServicesUI";

        if (settings.Launcher.Type.Equals("Playnite", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Playnite.FullscreenApp";
            yield return "Playnite.DesktopApp";
        }
        else if (settings.Launcher.Type.Equals("Steam", StringComparison.OrdinalIgnoreCase))
        {
            yield return "steam";
        }
        else if (settings.Launcher.Type.Equals("XboxApp", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Xbox";
            yield return "XboxPcApp";
            yield return "GamingServicesUI";
        }

        var executablePath = PathExpander.Expand(settings.Launcher.ExecutablePath);
        var fileName = Path.GetFileNameWithoutExtension(executablePath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            yield return fileName;
        }
    }

    private async Task CloseLauncherAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var processNames = GetLauncherProcessNames(settings).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _logger.LogInformation(
            "WORK MODE [{TransitionId}] Awaiting=LauncherShutdown Timeout={TimeoutMs}ms TokenCanBeCanceled={TokenCanBeCanceled} TokenIsCancellationRequested={TokenIsCancellationRequested}.",
            WorkModeTransitionContext.CurrentId ?? "-",
            LauncherShutdownTimeout.TotalMilliseconds,
            cancellationToken.CanBeCanceled,
            cancellationToken.IsCancellationRequested);

        foreach (var processName in processNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero && process.CloseMainWindow())
                    {
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        for (var attempt = 0; stopwatch.Elapsed < LauncherShutdownTimeout; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "WORK MODE [{TransitionId}] CANCELLATION DETECTED Operation=LauncherShutdown ExceptionType={ExceptionType} CancellationTokenIsCancellationRequested={CancellationTokenIsCancellationRequested} ExternalTokenIsCancellationRequested={ExternalTokenIsCancellationRequested} TimeoutTokenIsCancellationRequested={TimeoutTokenIsCancellationRequested} ElapsedMs={ElapsedMs}.",
                    WorkModeTransitionContext.CurrentId ?? "-",
                    typeof(OperationCanceledException).FullName,
                    cancellationToken.IsCancellationRequested,
                    cancellationToken.IsCancellationRequested,
                    false,
                    stopwatch.ElapsedMilliseconds);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var remaining = processNames.SelectMany(Process.GetProcessesByName).ToArray();
            if (remaining.Length == 0)
            {
                _logger.LogInformation(
                    "WORK MODE [{TransitionId}] LauncherShutdown completed. ElapsedMs={ElapsedMs}.",
                    WorkModeTransitionContext.CurrentId ?? "-",
                    stopwatch.ElapsedMilliseconds);
                return;
            }

            foreach (var process in remaining)
            {
                process.Dispose();
            }

            _logger.LogInformation(
                "WORK MODE [{TransitionId}] Awaiting=LauncherShutdownPoll Timeout=200ms TokenCanBeCanceled={TokenCanBeCanceled} TokenIsCancellationRequested={TokenIsCancellationRequested}.",
                WorkModeTransitionContext.CurrentId ?? "-",
                cancellationToken.CanBeCanceled,
                cancellationToken.IsCancellationRequested);
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        _logger.LogWarning(
            "WORK MODE [{TransitionId}] LauncherShutdown timed out as best-effort operation. TimeoutMs={TimeoutMs} ElapsedMs={ElapsedMs}; continuing Work Mode transition.",
            WorkModeTransitionContext.CurrentId ?? "-",
            LauncherShutdownTimeout.TotalMilliseconds,
            stopwatch.ElapsedMilliseconds);

        foreach (var processName in processNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    private static void MoveCursorToBottomRight()
    {
        var x = GetSystemMetrics(SmXVirtualScreen) + GetSystemMetrics(SmCxVirtualScreen) - 1;
        var y = GetSystemMetrics(SmYVirtualScreen) + GetSystemMetrics(SmCyVirtualScreen) - 1;
        SetCursorPos(x, y);
    }

    private static void HideSystemCursor()
    {
        var andMask = Enumerable.Repeat((byte)0xFF, CursorWidth * CursorHeight / 8).ToArray();
        var xorMask = new byte[CursorWidth * CursorHeight / 8];

        foreach (var cursorId in SystemCursorIds)
        {
            var cursor = CreateCursor(IntPtr.Zero, 0, 0, CursorWidth, CursorHeight, andMask, xorMask);
            if (cursor == IntPtr.Zero)
            {
                continue;
            }

            if (!SetSystemCursor(cursor, (uint)cursorId))
            {
                DestroyCursor(cursor);
            }
        }
    }

    private static void RestoreSystemCursors()
    {
        SystemParametersInfo(SpiSetCursors, 0, IntPtr.Zero, 0);
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

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
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassNameNative(IntPtr windowHandle, string className, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateCursor(
        IntPtr instance,
        int hotSpotX,
        int hotSpotY,
        int width,
        int height,
        byte[] andPlane,
        byte[] xorPlane);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetSystemCursor(IntPtr cursor, uint cursorId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyCursor(IntPtr cursor);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(int action, int parameter, IntPtr value, int flags);
}
