using GameShell.Core.Applications;
using GameShell.Core.Runtime;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Applications;

public sealed class WindowsGameSessionMonitor : IGameSessionMonitor, IDisposable
{
    private static readonly HashSet<string> ProtectedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "GameShell",
        "GameShell.Desktop",
        "explorer",
        "steam",
        "steamwebhelper",
        "Playnite.FullscreenApp",
        "Playnite.DesktopApp",
        "powershell",
        "pwsh",
        "cmd",
        "conhost",
        "wininit",
        "csrss",
        "dwm",
        "sihost",
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "SearchHost",
        "RuntimeBroker",
        "ApplicationFrameHost",
        "TextInputHost",
        "msedge",
        "msedgewebview2"
    };

    private static readonly TimeSpan CandidateStabilizationWindow = TimeSpan.FromSeconds(2);

    private readonly ILogger<WindowsGameSessionMonitor> _logger;
    private readonly object _gate = new();
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private string[] _processNames = [];
    private int? _detectedProcessId;
    private string? _detectedProcessName;
    private int? _candidateProcessId;
    private string? _candidateProcessName;
    private DateTimeOffset? _candidateObservedAt;

    public WindowsGameSessionMonitor(ILogger<WindowsGameSessionMonitor> logger)
    {
        _logger = logger;
    }

    public event EventHandler<GameSessionEventArgs>? GameDetected;
    public event EventHandler<GameSessionEventArgs>? GameExited;

    public async Task StartAsync(IReadOnlyCollection<string> processNames, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedNames = processNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => Path.GetFileNameWithoutExtension(name.Trim()))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        lock (_gate)
        {
            _processNames = normalizedNames;
            _detectedProcessId = null;
            _detectedProcessName = null;
            _candidateProcessId = null;
            _candidateProcessName = null;
            _candidateObservedAt = null;
            _monitorCancellation = new CancellationTokenSource();
            _monitorTask = MonitorAsync(_monitorCancellation.Token);
        }

        _logger.LogInformation(
            "[GAME SESSION MONITOR] Launcher={Launcher} ForegroundPid={ForegroundPid} ForegroundProcess={ForegroundProcess} CandidateGamePid={CandidateGamePid} CandidateGameProcess={CandidateGameProcess} LauncherPid={LauncherPid} Reason={Reason}.",
            "Playnite",
            GetForegroundProcessId(),
            GetForegroundProcessName(),
            "unknown",
            "unknown",
            FindLauncherProcessId(),
            normalizedNames.Length == 0 ? "auto foreground game detection" : "monitor started");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? monitorTask;
        CancellationToken monitorToken;
        lock (_gate)
        {
            var monitorCancellation = _monitorCancellation;
            monitorToken = monitorCancellation?.Token ?? CancellationToken.None;
            monitorCancellation?.Cancel();
            monitorTask = _monitorTask;
            _monitorCancellation = null;
            _monitorTask = null;
            _detectedProcessId = null;
            _detectedProcessName = null;
            _candidateProcessId = null;
            _candidateProcessName = null;
            _candidateObservedAt = null;
        }

        if (monitorTask is not null)
        {
            try
            {
                await monitorTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && monitorToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "WORK MODE [{TransitionId}] monitor shutdown cancellation consumed. Operation=GameSessionMonitorShutdown, ExceptionType={ExceptionType}, CancellationTokenIsCancellationRequested={CancellationTokenIsCancellationRequested}, ExternalTokenIsCancellationRequested={ExternalTokenIsCancellationRequested}, MonitorTokenIsCancellationRequested={MonitorTokenIsCancellationRequested}.",
                    WorkModeTransitionContext.CurrentId ?? "-",
                    exception.GetType().FullName,
                    exception.CancellationToken.IsCancellationRequested,
                    cancellationToken.IsCancellationRequested,
                    monitorToken.IsCancellationRequested);
            }
        }
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var foreground = GetForegroundProcessInfo();
            GameProcessInfo? candidate = null;
            EventHandler<GameSessionEventArgs>? handler = null;
            GameSessionEventArgs? eventArgs = null;
            string reason = string.Empty;
            var logCandidate = false;

            lock (_gate)
            {
                candidate = FindConfiguredGame();

                if (_detectedProcessId is not null)
                {
                    if (candidate is null || candidate.ProcessId != _detectedProcessId.Value)
                    {
                        var processId = _detectedProcessId.Value;
                        var processName = _detectedProcessName ?? string.Empty;
                        _detectedProcessId = null;
                        _detectedProcessName = null;
                        _candidateProcessId = null;
                        _candidateProcessName = null;
                        _candidateObservedAt = null;
                        handler = GameExited;
                        eventArgs = new GameSessionEventArgs(processId, processName);
                        reason = "tracked game process exited";
                        logCandidate = true;
                    }
                }
                else if (candidate is not null)
                {
                    if (_candidateProcessId != candidate.ProcessId)
                    {
                        _candidateProcessId = candidate.ProcessId;
                        _candidateProcessName = candidate.ProcessName;
                        _candidateObservedAt = DateTimeOffset.UtcNow;
                        reason = "new candidate observed";
                        logCandidate = true;
                    }
                    else if (_candidateObservedAt is not null && DateTimeOffset.UtcNow - _candidateObservedAt >= CandidateStabilizationWindow)
                    {
                        _detectedProcessId = candidate.ProcessId;
                        _detectedProcessName = candidate.ProcessName;
                        _candidateProcessId = null;
                        _candidateProcessName = null;
                        _candidateObservedAt = null;
                        handler = GameDetected;
                        eventArgs = new GameSessionEventArgs(candidate.ProcessId, candidate.ProcessName);
                        reason = "candidate stabilized";
                        logCandidate = true;
                    }
                }
                else
                {
                    _candidateProcessId = null;
                    _candidateProcessName = null;
                    _candidateObservedAt = null;
                }
            }

            if (logCandidate)
            {
                _logger.LogInformation(
                    "[GAME SESSION MONITOR] Launcher={Launcher} ForegroundPid={ForegroundPid} ForegroundProcess={ForegroundProcess} CandidateGamePid={CandidateGamePid} CandidateGameProcess={CandidateGameProcess} LauncherPid={LauncherPid} Reason={Reason}.",
                    "Playnite",
                    foreground?.ProcessId ?? -1,
                    foreground?.ProcessName ?? "unknown",
                    eventArgs?.ProcessId ?? _candidateProcessId ?? -1,
                    eventArgs?.ProcessName ?? _candidateProcessName ?? "unknown",
                    FindLauncherProcessId(),
                    reason);
            }

            if (handler is not null)
            {
                _logger.LogInformation("Configured game process state changed. PID={ProcessId}, Running={Running}.", eventArgs!.ProcessId, handler == GameDetected);
                handler(this, eventArgs);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    private GameProcessInfo? FindConfiguredGame()
    {
        string[] processNames;
        lock (_gate)
        {
            processNames = _processNames;
        }

        if (processNames.Length == 0)
        {
            return FindForegroundGame();
        }

        foreach (var processName in processNames)
        {
            try
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        if (!IsLikelyGameCandidate(process))
                        {
                            continue;
                        }

                        return new GameProcessInfo(process.Id, process.ProcessName);
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Could not inspect game process {ProcessName}.", processName);
            }
        }

        return null;
    }

    private static GameProcessInfo? FindForegroundGame()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(foregroundWindow, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (!IsLikelyGameCandidate(process))
            {
                return null;
            }

            return new GameProcessInfo(process.Id, process.ProcessName);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static bool IsLikelyGameCandidate(Process process)
    {
        if (process.HasExited)
        {
            return false;
        }

        var normalizedName = process.ProcessName ?? string.Empty;
        if (ProtectedProcessNames.Contains(normalizedName))
        {
            return false;
        }

        if (process.MainWindowHandle == IntPtr.Zero)
        {
            return false;
        }

        if (!IsWindowVisible(process.MainWindowHandle))
        {
            return false;
        }

        return true;
    }

    private static int? FindLauncherProcessId()
    {
        foreach (var processName in new[] { "Playnite.FullscreenApp", "Playnite.DesktopApp" })
        {
            var process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process is not null)
            {
                var id = process.Id;
                process.Dispose();
                return id;
            }
        }

        return null;
    }

    private static int? GetForegroundProcessId()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(handle, out var processId);
        return processId == 0 ? null : (int)processId;
    }

    private static string? GetForegroundProcessName()
    {
        var foregroundProcessId = GetForegroundProcessId();
        if (foregroundProcessId is null)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById(foregroundProcessId.Value).ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }

    private static ForegroundProcessInfo? GetForegroundProcessInfo()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            var process = Process.GetProcessById((int)processId);
            return new ForegroundProcessInfo((int)processId, process.ProcessName);
        }
        catch
        {
            return new ForegroundProcessInfo((int)processId, "unknown");
        }
    }

    private sealed record ForegroundProcessInfo(int ProcessId, string ProcessName);
    private sealed record GameProcessInfo(int ProcessId, string ProcessName);

    public void Dispose()
    {
        _monitorCancellation?.Cancel();
        _monitorCancellation?.Dispose();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
}
