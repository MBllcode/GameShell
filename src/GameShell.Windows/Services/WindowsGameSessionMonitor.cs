using System.Diagnostics;
using System.Runtime.InteropServices;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Services;

public sealed class WindowsGameSessionMonitor : IGameSessionMonitor, IDisposable
{
    private readonly ILogger<WindowsGameSessionMonitor> _logger;
    private readonly object _gate = new();
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private string[] _processNames = [];
    private int? _detectedProcessId;
    private string? _detectedProcessName;

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
            _monitorCancellation = new CancellationTokenSource();
            _monitorTask = MonitorAsync(_monitorCancellation.Token);
        }
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
            var detected = FindConfiguredGame();
            EventHandler<GameSessionEventArgs>? handler = null;
            GameSessionEventArgs? eventArgs = null;
            var isGameRunning = false;
            lock (_gate)
            {
                if (detected is not null && detected.ProcessId == _detectedProcessId)
                {
                    detected = null;
                }
                else if (detected is not null)
                {
                    _detectedProcessId = detected.ProcessId;
                    _detectedProcessName = detected.ProcessName;
                    handler = GameDetected;
                    eventArgs = new GameSessionEventArgs(detected.ProcessId, detected.ProcessName);
                    isGameRunning = true;
                }
                else if (_detectedProcessId is not null)
                {
                    var processId = _detectedProcessId.Value;
                    var processName = _detectedProcessName ?? string.Empty;
                    _detectedProcessId = null;
                    _detectedProcessName = null;
                    handler = GameExited;
                    eventArgs = new GameSessionEventArgs(processId, processName);
                }
            }

            if (handler is not null)
            {
                _logger.LogInformation("Configured game process state changed. PID={ProcessId}, Running={Running}.", eventArgs!.ProcessId, isGameRunning);
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

        foreach (var processName in processNames)
        {
            try
            {
                if (Process.GetProcessesByName(processName).Any())
                {
                    var process = Process.GetProcessesByName(processName).FirstOrDefault();
                    if (process is not null)
                    {
                        var result = new GameProcessInfo(process.Id, process.ProcessName);
                        process.Dispose();
                        return result;
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

    private sealed record GameProcessInfo(int ProcessId, string ProcessName);

    public void Dispose()
    {
        _monitorCancellation?.Cancel();
        _monitorCancellation?.Dispose();
    }

}
