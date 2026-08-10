using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Services;

public sealed class ConsoleSessionManager : IConsoleSessionManager, IDisposable
{
    private readonly IModeOrchestrator _modeOrchestrator;
    private readonly ISettingsService _settingsService;
    private readonly IGameSessionMonitor _gameSessionMonitor;
    private readonly ILauncherRecoveryStrategy _launcherRecoveryStrategy;
    private readonly ILogger<ConsoleSessionManager> _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _stateGate = new();
    private ConsoleSessionSnapshot _snapshot = new();
    private bool _disposed;

    public ConsoleSessionManager(
        IModeOrchestrator modeOrchestrator,
        ISettingsService settingsService,
        IGameSessionMonitor gameSessionMonitor,
        ILauncherRecoveryStrategy launcherRecoveryStrategy,
        ILogger<ConsoleSessionManager> logger)
    {
        _modeOrchestrator = modeOrchestrator;
        _settingsService = settingsService;
        _gameSessionMonitor = gameSessionMonitor;
        _launcherRecoveryStrategy = launcherRecoveryStrategy;
        _logger = logger;
        _gameSessionMonitor.GameDetected += OnGameDetected;
        _gameSessionMonitor.GameExited += OnGameExited;
    }

    public ConsoleSessionSnapshot GetSnapshot()
    {
        lock (_stateGate)
        {
            return _snapshot;
        }
    }

    public Task<ModeResult> EnterConsoleAsync(CancellationToken cancellationToken = default)
    {
        return EnterAsync(null, () => _modeOrchestrator.EnterGameShellAsync(cancellationToken), cancellationToken);
    }

    public Task<ModeResult> EnterProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        return EnterAsync(profileName, () => _modeOrchestrator.EnterGameShellWithDisplayProfileAsync(profileName, cancellationToken), cancellationToken);
    }

    public async Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
    {
        if (!await _operationLock.WaitAsync(0, cancellationToken))
        {
            return AlreadyRunning("Inna operacja sesji już trwa.");
        }

        try
        {
            var current = GetSnapshot();
            if (current.State is ConsoleSessionState.Idle)
            {
                return await _modeOrchestrator.EnterWorkModeAsync(cancellationToken);
            }

            SetState(ConsoleSessionState.ExitingToWorkMode, current.ProfileName, null);
            await _gameSessionMonitor.StopAsync(cancellationToken);
            var result = await _modeOrchestrator.EnterWorkModeAsync(cancellationToken);
            if (result.Success)
            {
                SetState(ConsoleSessionState.Idle, null, null);
            }
            else
            {
                SetState(ConsoleSessionState.Faulted, current.ProfileName, result.Message);
            }

            return result;
        }
        catch (Exception exception)
        {
            return Fail(currentProfileName: GetSnapshot().ProfileName, exception);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<ModeResult> EnterAsync(
        string? profileName,
        Func<Task<ModeResult>> activate,
        CancellationToken cancellationToken)
    {
        if (!await _operationLock.WaitAsync(0, cancellationToken))
        {
            return AlreadyRunning("Inna operacja sesji już trwa.");
        }

        try
        {
            var current = GetSnapshot();
            if (current.State is not ConsoleSessionState.Idle and not ConsoleSessionState.Faulted)
            {
                return AlreadyRunning("Sesja konsolowa jest już aktywna.");
            }

            SetState(ConsoleSessionState.EnteringProfile, profileName, null);
            var result = await activate();
            if (!result.Success)
            {
                SetState(ConsoleSessionState.Faulted, profileName, result.Message);
                return result;
            }

            var settings = await _settingsService.LoadAsync(cancellationToken);
            var behaviour = string.IsNullOrWhiteSpace(profileName)
                ? settings.PcGaming
                : settings.Display.PcGamingProfileConfigurations
                    .FirstOrDefault(configuration => string.Equals(configuration.ProfileName, profileName, StringComparison.OrdinalIgnoreCase))?.Behaviour
                    ?? settings.PcGaming;
            var processNames = behaviour.GameProcessNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            await _gameSessionMonitor.StartAsync(processNames, cancellationToken);
            SetState(ConsoleSessionState.LauncherActive, profileName, null);
            _logger.LogInformation("Console session entered for profile {ProfileName}.", profileName ?? "Console");
            return result;
        }
        catch (Exception exception)
        {
            return Fail(profileName, exception);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void OnGameDetected(object? sender, GameSessionEventArgs args)
    {
        lock (_stateGate)
        {
            if (_snapshot.State == ConsoleSessionState.LauncherActive)
            {
                _snapshot = _snapshot with { State = ConsoleSessionState.GameActive, ChangedAt = DateTimeOffset.Now };
                _logger.LogInformation("Game detected PID={ProcessId}; console session is now active.", args.ProcessId);
            }
        }
    }

    private async void OnGameExited(object? sender, GameSessionEventArgs args)
    {
        if (!await _operationLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            if (GetSnapshot().State != ConsoleSessionState.GameActive)
            {
                return;
            }

            var profileName = GetSnapshot().ProfileName;
            _logger.LogInformation("Game exited. PID={ProcessId}, Process={ProcessName}.", args.ProcessId, args.ProcessName);
            SetState(ConsoleSessionState.ReturningToLauncher, profileName, null);
            _logger.LogInformation("Game exited; waiting for launcher return.");
            var settings = await _settingsService.LoadAsync();
            var result = await _launcherRecoveryStrategy.RecoverAsync(settings, profileName);
            if (result.Success)
            {
                SetState(ConsoleSessionState.LauncherActive, profileName, null);
                _logger.LogInformation("{RecoveryMessage}", result.Message);
            }
            else
            {
                SetState(ConsoleSessionState.Faulted, GetSnapshot().ProfileName, result.Message);
            }
        }
        catch (Exception exception)
        {
            SetState(ConsoleSessionState.Faulted, GetSnapshot().ProfileName, exception.Message);
            _logger.LogError(exception, "Returning to launcher failed.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void SetState(ConsoleSessionState state, string? profileName, string? error)
    {
        lock (_stateGate)
        {
            _snapshot = new ConsoleSessionSnapshot
            {
                State = state,
                ProfileName = profileName,
                LastError = error,
                ChangedAt = DateTimeOffset.Now
            };
        }

        _logger.LogInformation("Console session state changed to {State}.", state);
    }

    private ModeResult Fail(string? currentProfileName, Exception exception)
    {
        SetState(ConsoleSessionState.Faulted, currentProfileName, exception.Message);
        _logger.LogError(exception, "Console session operation failed.");
        return new ModeResult
        {
            Status = ModeResultStatus.Failed,
            CurrentState = ModeState.Error,
            Message = exception.Message
        };
    }

    private static ModeResult AlreadyRunning(string message)
    {
        return new ModeResult
        {
            Status = ModeResultStatus.AlreadyRunning,
            CurrentState = ModeState.Error,
            Message = message
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gameSessionMonitor.GameDetected -= OnGameDetected;
        _gameSessionMonitor.GameExited -= OnGameExited;
        _operationLock.Dispose();
    }
}
