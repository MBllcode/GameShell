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

    public WindowsLauncherRecoveryStrategy(
        ILogger<WindowsLauncherRecoveryStrategy> logger,
        LauncherFocusRecoveryCoordinator? focusRecoveryCoordinator = null)
    {
        _logger = logger;
        _focusRecoveryCoordinator = focusRecoveryCoordinator ?? new LauncherFocusRecoveryCoordinator();
    }

    public async Task<LauncherRecoveryResult> RecoverAsync(
        AppSettings settings,
        string? profileName,
        int? gameProcessId = null,
        CancellationToken cancellationToken = default)
    {
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

        var restarted = StartLauncher(launcher);
        if (!restarted)
        {
            _logger.LogError("Launcher recovery failed: launcher could not be started.");
            return LauncherRecoveryResult.Failed("Nie udało się uruchomić launchera.");
        }

        _logger.LogInformation("Launcher restarted.");
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
        return settings.Display.PcGamingProfileConfigurations.FirstOrDefault(configuration =>
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

    private static bool StartLauncher(LauncherSettings launcher)
    {
        try
        {
            if (launcher.Type.Equals("Steam", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(launcher.ExecutablePath) ? "steam://open/bigpicture" : launcher.ExecutablePath,
                    UseShellExecute = true
                });
                return true;
            }

            if (launcher.Type.Equals("XboxApp", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "shell:AppsFolder\\Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App",
                    UseShellExecute = true
                });
                return true;
            }

            var executablePath = Environment.ExpandEnvironmentVariables(launcher.ExecutablePath);
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath)) return false;
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = launcher.Arguments,
                UseShellExecute = false
            });
            return true;
        }
        catch
        {
            return false;
        }
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
                        _logger.LogInformation("Launcher process found. PID={ProcessId}.", process.Id);
                        _logger.LogInformation("Launcher window found. HWND={WindowHandle}.", FormatHandle(mainWindowHandle));
                        return new LauncherDiagnostics(
                            process.Id,
                            mainWindowHandle,
                            foregroundWindow,
                            foregroundProcessId,
                            activeWindow,
                            IsWindowVisible(mainWindowHandle),
                            IsWindowEnabled(mainWindowHandle),
                            IsIconic(mainWindowHandle),
                            process.Responding,
                            foregroundWindow == mainWindowHandle);
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
            "Launcher focus diagnostics ({Phase}): LauncherWindowFound=true, HWND={MainHwnd}, Visible={Visible}, Minimized={Minimized}, Responding={Responding}, ForegroundBeforePid={ForegroundBeforePid}, ForegroundPid={ForegroundPid}, PlaynitePid={LauncherPid}, ForegroundConfirmed={IsForeground}.",
            phase,
            FormatHandle(diagnostics.MainWindowHandle),
            diagnostics.IsVisible,
            diagnostics.IsMinimized,
            diagnostics.IsResponding,
            diagnostics.ForegroundProcessId,
            diagnostics.ForegroundProcessId,
            diagnostics.ProcessId,
            diagnostics.IsForeground);
    }

    private void LogFocusInspection(string phase, LauncherFocusInspection inspection)
    {
        _logger.LogInformation(
            "Launcher focus diagnostics ({Phase}): LauncherWindowFound={WindowFound}, HWND={Hwnd}, Visible={Visible}, Minimized={Minimized}, Responding={Responding}, ForegroundAfterPid={ForegroundAfterPid}, ForegroundPid={ForegroundPid}, PlaynitePid={PlaynitePid}, ForegroundConfirmed={ForegroundConfirmed}.",
            phase,
            inspection.WindowExists,
            FormatHandle(inspection.WindowHandle),
            inspection.IsVisible,
            inspection.IsMinimized,
            inspection.IsResponding,
            inspection.ForegroundProcessId,
                inspection.ForegroundProcessId,
            inspection.ProcessId,
            inspection.IsForeground);
            }

    private void TryRecoverFocus(IntPtr launcherWindow)
    {
        if (launcherWindow == IntPtr.Zero)
        {
            return;
        }

        _logger.LogInformation("Focus recovery attempt: restoring and foregrounding Playnite HWND={MainHwnd}.", FormatHandle(launcherWindow));
        ShowWindow(launcherWindow, SwRestore);
        BringWindowToTop(launcherWindow);
        SetForegroundWindow(launcherWindow);
        _logger.LogInformation("Focus recovery SetForegroundWindow/BringWindowToTop calls completed; SetFocus skipped because this background thread does not own Playnite's UI thread.");
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
        bool IsVisible,
        bool IsEnabled,
        bool IsMinimized,
        bool IsResponding,
        bool IsForeground)
    {
        public bool IsHealthy => IsVisible && IsEnabled && !IsMinimized && IsResponding;

        public LauncherFocusInspection ToFocusInspection() => new(
            ProcessId,
            MainWindowHandle,
            MainWindowHandle != IntPtr.Zero,
            IsVisible,
            IsMinimized,
            IsResponding,
            ForegroundProcessId);
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
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
}
