using System.Diagnostics;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Services;

public sealed class NativeModeWorkflowService : IModeWorkflowService
{
    private static readonly TimeSpan LauncherShutdownTimeout = TimeSpan.FromSeconds(2);
    private readonly ILogger<NativeModeWorkflowService> _logger;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly IWindowsCursorService _cursorService;
    private readonly IWindowsWindowManagementService _windowManagementService;

    public NativeModeWorkflowService(
        ILogger<NativeModeWorkflowService> logger,
        IRuntimeStateService runtimeStateService,
        IWindowsCursorService cursorService,
        IWindowsWindowManagementService windowManagementService)
    {
        _logger = logger;
        _runtimeStateService = runtimeStateService;
        _cursorService = cursorService;
        _windowManagementService = windowManagementService;
    }

    public async Task<ModeWorkflowResult> PrepareGamingWorkflowAsync(
        AppSettings settings,
        GameShellMode mode,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var behaviour = mode == GameShellMode.GamingProfile ? settings.Gaming : settings.GameShell;

        await TryStepAsync("hide desktop icons", warnings, async () =>
        {
            if (behaviour.HideDesktopIcons)
            {
                await RetryAsync(() => _windowManagementService.SetDesktopIconsVisible(false), cancellationToken);
            }
        }, cancellationToken);

        await TryStepAsync("close visible windows", warnings, async () =>
        {
            if (behaviour.CloseVisibleWindows)
            {
                _windowManagementService.CloseVisibleWindows(behaviour);
                _windowManagementService.CloseExplorerWindows(behaviour);
                await Task.Delay(700, cancellationToken);
            }
        }, cancellationToken);

        await TryStepAsync("minimize remaining windows", warnings, async () =>
        {
            if (behaviour.MinimizeRemainingWindows)
            {
                _windowManagementService.MinimizeRemainingWindows(behaviour);
                _windowManagementService.MinimizeCurrentWindow();
                _windowManagementService.MinimizeExplorerWindows();
            }

            await Task.CompletedTask;
        }, cancellationToken);

        await TryStepAsync("hide taskbars", warnings, async () =>
        {
            if (behaviour.HideTaskbar)
            {
                await RetryAsync(() => _windowManagementService.SetTaskbarsVisible(false), cancellationToken);
            }
        }, cancellationToken);

        await TryStepAsync("move cursor", warnings, async () =>
        {
            if (behaviour.MoveCursorToCorner)
            {
                _cursorService.MoveToBottomRight();
            }

            await Task.CompletedTask;
        }, cancellationToken);

        await TryStepAsync("hide cursor", warnings, async () =>
        {
            if (behaviour.HideCursor)
            {
                _cursorService.Hide();
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
        var behaviour = mode == GameShellMode.GamingProfile ? settings.Gaming : settings.GameShell;
        Process? launcherProcess = null;

        await TryStepAsync("minimize remaining windows after display transition", warnings, async () =>
        {
            if (behaviour.MinimizeRemainingWindows)
            {
                _windowManagementService.MinimizeRemainingWindows(behaviour);
                _windowManagementService.MinimizeCurrentWindow();
                _windowManagementService.MinimizeExplorerWindows();
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
                _cursorService.MoveToBottomRight();
            }

            await Task.CompletedTask;
        }, cancellationToken);

        await TryStepAsync("hide cursor", warnings, async () =>
        {
            if (behaviour.HideCursor)
            {
                _cursorService.Hide();
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
            _cursorService.Restore();
            await Task.CompletedTask;
        }, cancellationToken);

        await TryStepAsync("show taskbars", warnings, async () =>
        {
            if (settings.WorkMode.ShowTaskbars)
            {
                await RetryAsync(() => _windowManagementService.SetTaskbarsVisible(true), cancellationToken);
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
            if (_windowManagementService.SetDesktopIconsVisible(true))
            {
                return;
            }

            await RetryAsync(() => _windowManagementService.SetDesktopIconsVisible(true), cancellationToken);
            var fallbackDelaySeconds = Math.Clamp(delaySeconds, 0, 5);
            if (fallbackDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(fallbackDelaySeconds), cancellationToken);
                await RetryAsync(() => _windowManagementService.SetDesktopIconsVisible(true), cancellationToken);
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

    private void BringLauncherToForeground(AppSettings settings, Process? launcherProcess)
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

        _windowManagementService.RestoreAndFocus(process.MainWindowHandle);
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

}
