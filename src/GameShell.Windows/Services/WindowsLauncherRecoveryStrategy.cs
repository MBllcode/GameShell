using System.Diagnostics;
using System.Runtime.InteropServices;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Services;

public sealed class WindowsLauncherRecoveryStrategy : ILauncherRecoveryStrategy
{
    private const int SwRestore = 9;
    private readonly ILogger<WindowsLauncherRecoveryStrategy> _logger;
    private readonly LauncherFocusRecoveryCoordinator _focusRecoveryCoordinator;
    private readonly ILauncherProcessService _launcherProcessService;

    public WindowsLauncherRecoveryStrategy(
        ILogger<WindowsLauncherRecoveryStrategy> logger,
        ILauncherProcessService launcherProcessService,
        LauncherFocusRecoveryCoordinator? focusRecoveryCoordinator = null)
    {
        _logger = logger;
        _launcherProcessService = launcherProcessService;
        _focusRecoveryCoordinator = focusRecoveryCoordinator ?? new LauncherFocusRecoveryCoordinator();
    }

    public async Task<LauncherRecoveryResult> RecoverAsync(
        AppSettings settings,
        string? profileName,
        int? gameProcessId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[PLAYNITE RETURN START] GamePid={GamePid} PlaynitePid={PlaynitePid} Reason={Reason}.", gameProcessId, "unknown", "GameExited");
        _logger.LogInformation("PLAYNITE POST-GAME RECOVERY START");
        await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);

        var launcher = ResolveLauncher(settings, profileName);
        var processNames = GetLauncherProcessNames(launcher).ToArray();
        var diagnostics = FindLauncherDiagnostics(processNames);
        _logger.LogInformation(
            "GamePid={GamePid}, PlaynitePid={PlaynitePid}, WaitedForOwnRecoveryMs={WaitedForOwnRecoveryMs}.",
            gameProcessId,
            diagnostics?.ProcessId,
            1500);
        _logger.LogInformation("[PLAYNITE ACTIVATION STATE] GamePid={GamePid} PlaynitePid={PlaynitePid} Window={WindowHandle} IsForeground={IsForeground} IsHealthy={IsHealthy} Reason={Reason}.",
            gameProcessId,
            diagnostics?.ProcessId ?? -1,
            diagnostics is null ? "unknown" : FormatHandle(diagnostics.MainWindowHandle),
            diagnostics?.IsForeground ?? false,
            diagnostics?.IsHealthy ?? false,
            diagnostics is null ? "launcher not found" : "diagnostics captured");
        if (diagnostics is not null)
        {
            LogDiagnostics("launcher return", diagnostics);
        }
        else
        {
            _logger.LogInformation("LauncherWindowFound=false, ForegroundBefore=unknown, Result=FAILED.");
        }

        if (diagnostics is { IsHealthy: true, IsForeground: true })
        {
            _logger.LogInformation("Result=SUCCESS; healthy Playnite return required no recovery.");
            return LauncherRecoveryResult.Recovered("Launcher returned normally");
        }

        var focusResult = await _focusRecoveryCoordinator.RecoverAsync(
            () => FindLauncherDiagnostics(processNames)?.ToFocusInspection(),
            TryRecoverFocus,
            (attempt, inspection) =>
            {
                if (inspection is not null)
                {
                    LogFocusInspection($"attempt {attempt}/2", inspection);
                }

                _logger.LogInformation(
                    "Attempt={Attempt}/2, ForegroundAfter={ForegroundAfter}, ForegroundConfirmed={ForegroundConfirmed}, Result={Result}.",
                    attempt == 0 ? "0" : attempt,
                    inspection?.ForegroundProcessId,
                    inspection?.IsForeground,
                    inspection is { IsHealthy: true, IsForeground: true } ? "SUCCESS" : "FAILED");
            },
            cancellationToken);

        if (focusResult.Success)
        {
            _logger.LogInformation("PLAYNITE POST-GAME RECOVERY END Result=SUCCESS.");
            return LauncherRecoveryResult.Recovered("Launcher recovered after focus recovery");
        }

        _logger.LogWarning("PLAYNITE RECOVERY FALLBACK RESTART");
        CloseLauncherProcesses(processNames);
        await WaitForLauncherExitAsync(processNames, cancellationToken);

        var remaining = FindLauncherProcesses(processNames);
        if (remaining.Count > 0)
        {
            _logger.LogWarning("Graceful launcher close failed; terminating launcher processes only.");
            foreach (var process in remaining)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: false);
                }
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    _logger.LogWarning(exception, "Launcher terminate failed. PID={ProcessId}.", process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }

            await WaitForLauncherExitAsync(processNames, cancellationToken);
        }

        var restartResult = StartLauncher(launcher);
        if (!restartResult.Success)
        {
            _logger.LogError(
                "Launcher recovery failed: launcher could not be started. Win32Error={Win32Error} Reason={Reason}.",
                restartResult.Win32ErrorCode,
                restartResult.FailureReason);
            return LauncherRecoveryResult.Failed("Nie udało się uruchomić launchera.");
        }

        _logger.LogInformation("Launcher restarted. Pid={ProcessId}.", restartResult.ProcessId);
        var recovered = await WaitForHealthyLauncherAsync(processNames, cancellationToken);
        if (recovered)
        {
            _logger.LogInformation("Launcher recovery complete.");
            return LauncherRecoveryResult.RestartedLauncher("Launcher został uruchomiony ponownie i odzyskał poprawny stan.");
        }

        _logger.LogError("Launcher recovery failed.");
        return LauncherRecoveryResult.Failed("Launcher nie odzyskał poprawnego stanu po restarcie.");
    }

    private static LauncherSettings ResolveLauncher(AppSettings settings, string? profileName)
    {
        return settings.Display.GamingProfileConfigurations.FirstOrDefault(configuration =>
            string.Equals(configuration.ProfileName, profileName, StringComparison.OrdinalIgnoreCase))?.Launcher
            ?? settings.Launcher;
    }

    private static IEnumerable<string> GetLauncherProcessNames(LauncherSettings launcher)
    {
        if (launcher.Type.Equals("Playnite", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Playnite.FullscreenApp";
            yield return "Playnite.DesktopApp";
            yield break;
        }

        var executableName = Path.GetFileNameWithoutExtension(Environment.ExpandEnvironmentVariables(launcher.ExecutablePath));
        if (!string.IsNullOrWhiteSpace(executableName))
        {
            yield return executableName;
        }
    }

    private void CloseLauncherProcesses(IReadOnlyCollection<string> processNames)
    {
        foreach (var process in FindLauncherProcesses(processNames))
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero && process.CloseMainWindow())
                {
                    _logger.LogInformation("Graceful launcher close requested. PID={ProcessId}.", process.Id);
                }
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private async Task WaitForLauncherExitAsync(IReadOnlyCollection<string> processNames, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (!FindLauncherProcesses(processNames).Any()) return;
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }

    private async Task<bool> WaitForHealthyLauncherAsync(IReadOnlyCollection<string> processNames, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var diagnostics = FindLauncherDiagnostics(processNames);
            if (diagnostics is not null)
            {
                _logger.LogInformation("Launcher process found. PID={ProcessId}.", diagnostics.ProcessId);
                _logger.LogInformation("Launcher window found. HWND={WindowHandle}.", FormatHandle(diagnostics.MainWindowHandle));
                if (!diagnostics.IsHealthy || !diagnostics.IsForeground)
                {
                    TryRecoverFocus(diagnostics.MainWindowHandle);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
                var afterRecovery = FindLauncherDiagnostics(processNames);
                if (afterRecovery is not null && afterRecovery.IsHealthy && afterRecovery.IsForeground)
                {
                    return true;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return false;
    }

    private ExternalProcessLaunchResult StartLauncher(LauncherSettings launcher)
    {
        return _launcherProcessService.StartStandardUser(launcher);
    }

    private LauncherDiagnostics? FindLauncherDiagnostics(IReadOnlyCollection<string> processNames)
    {
        foreach (var processName in processNames)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    var mainWindowHandle = FindTopLevelWindow(process.Id);
                    if (mainWindowHandle != IntPtr.Zero)
                    {
                        var foregroundWindow = GetForegroundWindow();
                        GetWindowThreadProcessId(foregroundWindow, out var foregroundProcessId);
                        var activeWindow = GetActiveWindow();
                        var focusWindow = GetFocus();
                        var guiThreadId = foregroundWindow != IntPtr.Zero ? GetWindowThreadProcessId(foregroundWindow, out var threadId) : 0;
                        var guiThreadInfo = default(GuiThreadInfo);
                        var guiThreadInfoReady = guiThreadId != 0 && GetGUIThreadInfo(guiThreadId, ref guiThreadInfo);
                        var captureWindow = guiThreadInfoReady ? guiThreadInfo.hwndCapture : IntPtr.Zero;
                        var foregroundMatchesWindow = IsWindowOrChild(foregroundWindow, mainWindowHandle);
                        var activeMatchesWindow = IsWindowOrChild(activeWindow, mainWindowHandle);
                        var focusMatchesWindow = IsWindowOrChild(focusWindow, mainWindowHandle);
                        var captureMatchesWindow = IsWindowOrChild(captureWindow, mainWindowHandle);
                        var isForeground = foregroundProcessId == (uint)process.Id &&
                            (foregroundMatchesWindow || activeMatchesWindow || focusMatchesWindow || captureMatchesWindow);

                        _logger.LogInformation("Launcher process found. PID={ProcessId}.", process.Id);
                        _logger.LogInformation("Launcher window found. HWND={WindowHandle}.", FormatHandle(mainWindowHandle));
                        _logger.LogInformation(
                            "[PLAYNITE ACTIVATION STATE] Window={WindowHandle}, Foreground={ForegroundHandle}, ForegroundPid={ForegroundPid}, Active={ActiveHandle}, Focus={FocusHandle}, Capture={CaptureHandle}, ParentMatch={ParentMatch}, ActiveMatch={ActiveMatch}, FocusMatch={FocusMatch}, CaptureMatch={CaptureMatch}, IsForegroundConfirmed={IsForeground}.",
                            FormatHandle(mainWindowHandle),
                            FormatHandle(foregroundWindow),
                            foregroundProcessId,
                            FormatHandle(activeWindow),
                            FormatHandle(focusWindow),
                            FormatHandle(captureWindow),
                            foregroundMatchesWindow,
                            activeMatchesWindow,
                            focusMatchesWindow,
                            captureMatchesWindow,
                            isForeground);

                        return new LauncherDiagnostics(
                            process.Id,
                            mainWindowHandle,
                            foregroundWindow,
                            foregroundProcessId,
                            activeWindow,
                            focusWindow,
                            captureWindow,
                            IsWindowVisible(mainWindowHandle),
                            IsWindowEnabled(mainWindowHandle),
                            IsIconic(mainWindowHandle),
                            process.Responding,
                            isForeground,
                            guiThreadInfoReady,
                            foregroundMatchesWindow,
                            activeMatchesWindow,
                            focusMatchesWindow);
                    }
                }
                catch
                {
                    // A process can exit while its window is inspected.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return null;
    }

    private void LogDiagnostics(string phase, LauncherDiagnostics diagnostics)
    {
        _logger.LogInformation(
            "Launcher focus diagnostics ({Phase}): LauncherWindowFound=true, HWND={MainHwnd}, Visible={Visible}, Minimized={Minimized}, Responding={Responding}, ForegroundBeforePid={ForegroundBeforePid}, ForegroundPid={ForegroundPid}, PlaynitePid={LauncherPid}, ForegroundConfirmed={IsForeground}, GuiThreadActive={GuiThreadActive}, ForegroundMatchesWindow={ForegroundMatchesWindow}, ActiveMatchesWindow={ActiveMatchesWindow}, FocusMatchesWindow={FocusMatchesWindow}.",
            phase,
            FormatHandle(diagnostics.MainWindowHandle),
            diagnostics.IsVisible,
            diagnostics.IsMinimized,
            diagnostics.IsResponding,
            diagnostics.ForegroundProcessId,
            diagnostics.ForegroundProcessId,
            diagnostics.ProcessId,
            diagnostics.IsForeground,
            diagnostics.GuiThreadActive,
            diagnostics.ForegroundMatchesWindow,
            diagnostics.ActiveMatchesWindow,
            diagnostics.FocusMatchesWindow);
    }

    private void LogFocusInspection(string phase, LauncherFocusInspection inspection)
    {
        _logger.LogInformation(
            "Launcher focus diagnostics ({Phase}): LauncherWindowFound={WindowFound}, HWND={Hwnd}, Visible={Visible}, Minimized={Minimized}, Responding={Responding}, ForegroundAfterPid={ForegroundAfterPid}, ForegroundPid={ForegroundPid}, PlaynitePid={PlaynitePid}, ForegroundConfirmed={ForegroundConfirmed}, ActiveWindow={ActiveWindow}, FocusWindow={FocusWindow}, CaptureWindow={CaptureWindow}, ActiveMatchesWindow={ActiveMatchesWindow}, FocusMatchesWindow={FocusMatchesWindow}.",
            phase,
            inspection.WindowExists,
            FormatHandle(inspection.WindowHandle),
            inspection.IsVisible,
            inspection.IsMinimized,
            inspection.IsResponding,
            inspection.ForegroundProcessId,
            inspection.ForegroundProcessId,
            inspection.ProcessId,
            inspection.IsForeground,
            FormatHandle(inspection.ActiveWindow),
            FormatHandle(inspection.FocusWindow),
            FormatHandle(inspection.CaptureWindow),
            inspection.ActiveMatchesWindow,
            inspection.FocusMatchesWindow);
    }

    private void TryRecoverFocus(IntPtr launcherWindow)
    {
        if (launcherWindow == IntPtr.Zero)
        {
            return;
        }

        _logger.LogInformation("[PLAYNITE ACTIVATION RECOVERY] Window={WindowHandle} Action={Action}.", FormatHandle(launcherWindow), "restore-and-foreground");
        _logger.LogInformation("Focus recovery attempt: restoring and foregrounding Playnite HWND={MainHwnd}.", FormatHandle(launcherWindow));
        ShowWindow(launcherWindow, SwRestore);
        BringWindowToTop(launcherWindow);
        SetForegroundWindow(launcherWindow);

        var foregroundWindow = GetForegroundWindow();
        GetWindowThreadProcessId(foregroundWindow, out var foregroundProcessId);
        var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out var _);
        var launcherThreadId = GetWindowThreadProcessId(launcherWindow, out var _);

        if (foregroundThreadId != 0 && launcherThreadId != 0 && foregroundThreadId != launcherThreadId)
        {
            _logger.LogInformation(
                "[PLAYNITE ACTIVATION RECOVERY] TargetWindow={WindowHandle}, ForegroundWindow={ForegroundWindow}, ForegroundPid={ForegroundPid}, LauncherThreadId={LauncherThreadId}, ForegroundThreadId={ForegroundThreadId}.",
                FormatHandle(launcherWindow),
                FormatHandle(foregroundWindow),
                foregroundProcessId,
                launcherThreadId,
                foregroundThreadId);

            if (AttachThreadInput(foregroundThreadId, launcherThreadId, true))
            {
                try
                {
                    SetForegroundWindow(launcherWindow);
                    SetActiveWindow(launcherWindow);
                    SetFocus(launcherWindow);
                    _logger.LogInformation("[PLAYNITE ACTIVATION RECOVERY] AttachThreadInput fallback activated the launcher window with a bounded foreground handoff.");
                }
                finally
                {
                    AttachThreadInput(foregroundThreadId, launcherThreadId, false);
                }
            }
        }

        _logger.LogInformation("Focus recovery SetForegroundWindow/BringWindowToTop calls completed; background-thread activation is bounded and verified by the next inspection pass.");
    }

    private static bool IsWindowOrChild(IntPtr candidate, IntPtr rootWindow)
    {
        if (candidate == IntPtr.Zero || rootWindow == IntPtr.Zero)
        {
            return false;
        }

        for (var current = candidate; current != IntPtr.Zero; current = GetParent(current))
        {
            if (current == rootWindow)
            {
                return true;
            }
        }

        return false;
    }

    private static IntPtr FindTopLevelWindow(int processId)
    {
        var candidates = new List<(IntPtr Handle, bool Visible, long Area)>();
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var ownerProcessId);
            if (ownerProcessId != processId)
            {
                return true;
            }

            if ((GetWindowLong(window, ExStyleIndex) & ToolWindowStyle) != 0)
            {
                return true;
            }

            if (!GetWindowRect(window, out var bounds))
            {
                return true;
            }

            var area = Math.Max(0, bounds.Right - bounds.Left) * (long)Math.Max(0, bounds.Bottom - bounds.Top);
            candidates.Add((window, IsWindowVisible(window), area));
            return true;
        }, IntPtr.Zero);
        return candidates
            .OrderByDescending(candidate => candidate.Visible)
            .ThenByDescending(candidate => candidate.Area)
            .Select(candidate => candidate.Handle)
            .FirstOrDefault();
    }

    private static string FormatHandle(IntPtr handle)
    {
        return $"0x{handle.ToInt64():X}";
    }

    private List<Process> FindLauncherProcesses(IReadOnlyCollection<string> processNames)
    {
        return processNames
            .SelectMany(Process.GetProcessesByName)
            .ToList();
    }

    private sealed record LauncherDiagnostics(
        int ProcessId,
        IntPtr MainWindowHandle,
        IntPtr ForegroundWindow,
        uint ForegroundProcessId,
        IntPtr ActiveWindow,
        IntPtr FocusWindow,
        IntPtr CaptureWindow,
        bool IsVisible,
        bool IsEnabled,
        bool IsMinimized,
        bool IsResponding,
        bool IsForeground,
        bool GuiThreadActive,
        bool ForegroundMatchesWindow,
        bool ActiveMatchesWindow,
        bool FocusMatchesWindow)
    {
        public bool IsHealthy => IsVisible && IsEnabled && !IsMinimized && IsResponding;

        public LauncherFocusInspection ToFocusInspection() => new(
            ProcessId,
            MainWindowHandle,
            MainWindowHandle != IntPtr.Zero,
            IsVisible,
            IsMinimized,
            IsResponding,
            ForegroundProcessId,
            ForegroundWindow,
            ActiveWindow,
            FocusWindow,
            CaptureWindow,
            GuiThreadActive,
            ForegroundMatchesWindow,
            ActiveMatchesWindow,
            FocusMatchesWindow);
    }

    private const int ExStyleIndex = -20;
    private const long ToolWindowStyle = 0x00000080L;

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint Size;
        public uint Flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public WindowRect rcCaret;
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out WindowRect rectangle);

    [DllImport("user32.dll")]
    private static extern long GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo threadInfo);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
}
