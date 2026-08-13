using System.Diagnostics;
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
    private long _sessionGeneration;
    private static long _transitionSequence;
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
            _logger.LogWarning("WORK MODE TRANSITION rejected because another session operation is running.");
            return AlreadyRunning("Inna operacja sesji już trwa.");
        }

        var transitionId = Interlocked.Increment(ref _transitionSequence).ToString();
        using var transitionContext = WorkModeTransitionContext.Begin(transitionId);
        using var transitionScope = _logger.BeginScope("WORK MODE TRANSITION [{TransitionId}]", transitionId);

        try
        {
            var current = GetSnapshot();
            var settings = await _settingsService.LoadAsync(cancellationToken);
            var activeLauncher = settings.Display.GamingProfileConfigurations
                .FirstOrDefault(configuration => string.Equals(configuration.ProfileName, current.ProfileName, StringComparison.OrdinalIgnoreCase))?.Launcher.Type
                ?? settings.Launcher.Type;
            _logger.LogInformation(
                "WORK MODE TRANSITION START [CurrentSessionState={CurrentSessionState}, ActiveProfile={ActiveProfile}, ActiveLauncher={ActiveLauncher}, ActiveGamePid={ActiveGamePid}, DisplayState={DisplayState}].",
                current.State,
                current.ProfileName ?? "(none)",
                activeLauncher,
                "unknown",
                "runtime state queried by display transition");

            if (current.State is ConsoleSessionState.Idle)
            {
                _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 1 START session is already idle; delegating display/work restore.", transitionId);
                var idleResult = await _modeOrchestrator.EnterWorkModeAsync(cancellationToken);
                if (!idleResult.Success)
                {
                    SetState(ConsoleSessionState.Faulted, current.ProfileName, idleResult.Message);
                    _logger.LogError("WORK MODE TRANSITION FAILED [Step=orchestrator, Message={Message}, CurrentSessionStateAfterFailure={CurrentSessionStateAfterFailure}].", idleResult.Message, ConsoleSessionState.Faulted);
                }
                else
                {
                    SetState(ConsoleSessionState.Idle, null, null);
                    var committedSnapshot = GetSnapshot();
                    _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 1 SUCCESS; CurrentSessionStateAfterSuccess={CurrentSessionStateAfterSuccess}.", transitionId, ConsoleSessionState.Idle);
                    _logger.LogInformation(
                        "[WORKMODE SESSION COMMIT] Timestamp={Timestamp} TransitionId={TransitionId} RuntimeMode=Work SessionState={SessionState} ActiveProfileName={ActiveProfileName} ActiveLauncher=null.",
                        DateTimeOffset.Now.ToString("O"),
                        transitionId,
                        committedSnapshot.State,
                        committedSnapshot.ProfileName ?? "null");
                }
                return idleResult;
            }

            AdvanceSessionGeneration();
            SetState(ConsoleSessionState.ExitingToWorkMode, current.ProfileName, null);
            _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 1 START stopping game session monitor.", transitionId);
            await _gameSessionMonitor.StopAsync(cancellationToken);
            _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 1 SUCCESS game session monitor stopped.", transitionId);

            _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 2 START orchestrator restore.", transitionId);
            var result = await _modeOrchestrator.EnterWorkModeAsync(cancellationToken);
            if (result.Success)
            {
                SetState(ConsoleSessionState.Idle, null, null);
                var committedSnapshot = GetSnapshot();
                _logger.LogInformation(
                    "[WORKMODE SESSION COMMIT] Timestamp={Timestamp} TransitionId={TransitionId} RuntimeMode=Work SessionState={SessionState} ActiveProfileName={ActiveProfileName} ActiveLauncher=null.",
                    DateTimeOffset.Now.ToString("O"),
                    transitionId,
                    committedSnapshot.State,
                    committedSnapshot.ProfileName ?? "null");
                _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 2 SUCCESS; CurrentSessionStateAfterSuccess={CurrentSessionStateAfterSuccess}.", transitionId, ConsoleSessionState.Idle);
            }
            else
            {
                SetState(ConsoleSessionState.Faulted, current.ProfileName, result.Message);
                _logger.LogError("WORK MODE TRANSITION FAILED [Step=orchestrator, ExceptionType=ModeResult, Message={Message}, CurrentSessionStateAfterFailure={CurrentSessionStateAfterFailure}].", result.Message, ConsoleSessionState.Faulted);
            }

            return result;
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException canceledException)
            {
                const string cancellationMessage = "Operacja Work Mode została anulowana.";
                SetState(ConsoleSessionState.Faulted, GetSnapshot().ProfileName, cancellationMessage);
                _logger.LogWarning(
                    canceledException,
                    "WORK MODE TRANSITION CANCELLATION DETECTED [TransitionId={TransitionId}, Operation=ConsoleSessionManager, ExceptionType={ExceptionType}, CancellationTokenIsCancellationRequested={CancellationTokenIsCancellationRequested}, ExternalTokenIsCancellationRequested={ExternalTokenIsCancellationRequested}, TimeoutTokenIsCancellationRequested={TimeoutTokenIsCancellationRequested}, ElapsedMs=unknown].",
                    transitionId,
                    canceledException.GetType().FullName,
                    canceledException.CancellationToken.IsCancellationRequested,
                    cancellationToken.IsCancellationRequested,
                    false);
                return new ModeResult
                {
                    Status = ModeResultStatus.Cancelled,
                    CurrentState = ModeState.Unknown,
                    Message = cancellationMessage
                };
            }

            var result = Fail(currentProfileName: GetSnapshot().ProfileName, exception);
            var inner = exception.InnerException?.ToString() ?? "(none)";
            _logger.LogError(
                exception,
                "WORK MODE TRANSITION FAILED [Step=session, ExceptionType={ExceptionType}, Message={Message}, StackTrace={StackTrace}, InnerException={InnerException}, CurrentSessionStateAfterFailure={CurrentSessionStateAfterFailure}].",
                exception.GetType().FullName,
                exception.Message,
                exception.StackTrace ?? "(none)",
                inner,
                GetSnapshot().State);
            return result;
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
                ? settings.Gaming
                : settings.Display.GamingProfileConfigurations
                    .FirstOrDefault(configuration => string.Equals(configuration.ProfileName, profileName, StringComparison.OrdinalIgnoreCase))?.Behaviour
                    ?? settings.Gaming;
            var processNames = behaviour.GameProcessNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            await _gameSessionMonitor.StartAsync(processNames, cancellationToken);
            AdvanceSessionGeneration();
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
                SetStateLocked(ConsoleSessionState.LaunchingGame, _snapshot.ProfileName, null, "GameDetected", args.ProcessId, args.ProcessName, null, null);
                _logger.LogInformation("[GAME SESSION STATE] OldState={OldState} NewState={NewState} LauncherPid={LauncherPid} GamePid={GamePid} GameProcess={GameProcess} Reason={Reason}.", ConsoleSessionState.LauncherActive, ConsoleSessionState.LaunchingGame, "unknown", args.ProcessId, args.ProcessName, "GameDetected");
                SetStateLocked(ConsoleSessionState.GameActive, _snapshot.ProfileName, null, "GameDetectedConfirmed", args.ProcessId, args.ProcessName, null, null);
                _logger.LogInformation("[GAME SESSION STATE] OldState={OldState} NewState={NewState} LauncherPid={LauncherPid} GamePid={GamePid} GameProcess={GameProcess} Reason={Reason}.", ConsoleSessionState.LaunchingGame, ConsoleSessionState.GameActive, "unknown", args.ProcessId, args.ProcessName, "GameDetectedConfirmed");
                _logger.LogInformation("Game detected PID={ProcessId}; console session is now active.", args.ProcessId);
            }
            else
            {
                _logger.LogInformation("RUNTIME STATE UPDATE Source=GameDetected SessionId={SessionId} ignored because session state is {State}.", _snapshot.SessionGeneration, _snapshot.State);
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
            var callbackSessionGeneration = GetSnapshot().SessionGeneration;
            if (GetSnapshot().State != ConsoleSessionState.GameActive)
            {
                _logger.LogInformation("RUNTIME STATE UPDATE Source=GameExited SessionId={SessionId} ignored because session state is {State}.", callbackSessionGeneration, GetSnapshot().State);
                return;
            }

            var profileName = GetSnapshot().ProfileName;
            _logger.LogInformation("Game exited. PID={ProcessId}, Process={ProcessName}.", args.ProcessId, args.ProcessName);
            SetState(ConsoleSessionState.ReturningToLauncher, profileName, null, args.ProcessId, args.ProcessName);
            _logger.LogInformation("[PLAYNITE RETURN START] GamePid={GamePid} PlaynitePid={PlaynitePid} Reason={Reason}.", args.ProcessId, "unknown", "GameExited");
            _logger.LogInformation("Game exited; waiting for launcher return.");
            var settings = await _settingsService.LoadAsync();
            var result = await _launcherRecoveryStrategy.RecoverAsync(settings, profileName, args.ProcessId);
            if (GetSnapshot().SessionGeneration != callbackSessionGeneration || GetSnapshot().State != ConsoleSessionState.ReturningToLauncher)
            {
                _logger.LogInformation("RUNTIME STATE UPDATE Source=GameExited SessionId={SessionId} ignored because session generation changed.", callbackSessionGeneration);
                return;
            }
            if (result.Success)
            {
                SetState(ConsoleSessionState.LauncherActive, profileName, null, null, null);
                _logger.LogInformation("{RecoveryMessage}", result.Message);
            }
            else
            {
                SetState(ConsoleSessionState.Faulted, GetSnapshot().ProfileName, result.Message, null, null);
            }
        }
        catch (Exception exception)
        {
            SetState(ConsoleSessionState.Faulted, GetSnapshot().ProfileName, exception.Message, null, null);
            _logger.LogError(exception, "Returning to launcher failed.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void SetState(ConsoleSessionState state, string? profileName, string? error, int? gamePid = null, string? gameProcess = null)
    {
        lock (_stateGate)
        {
            SetStateLocked(state, profileName, error, "ConsoleSessionManager", gamePid, gameProcess, null, null);
        }
    }

    private void SetStateLocked(
        ConsoleSessionState state,
        string? profileName,
        string? error,
        string source,
        int? gamePid,
        string? gameProcess,
        int? launcherPid,
        string? launcherProcess)
    {
        var previous = _snapshot;
        var nextGamePid = gamePid ?? previous.ActiveGamePid;
        var nextGameProcess = gameProcess ?? previous.ActiveGameProcess;
        var nextLauncherPid = launcherPid ?? previous.LauncherPid;
        var nextLauncherProcess = launcherProcess ?? previous.LauncherProcess;

        if (state is not ConsoleSessionState.GameActive and not ConsoleSessionState.LaunchingGame and not ConsoleSessionState.ReturningToLauncher)
        {
            nextGamePid = null;
            nextGameProcess = null;
        }

        _snapshot = new ConsoleSessionSnapshot
        {
            State = state,
            SessionGeneration = _sessionGeneration,
            ProfileName = profileName,
            LastError = error,
            ActiveGamePid = nextGamePid,
            ActiveGameProcess = nextGameProcess,
            LauncherPid = nextLauncherPid,
            LauncherProcess = nextLauncherProcess,
            ChangedAt = DateTimeOffset.Now
        };
        _logger.LogInformation("[GAME SESSION STATE] OldState={OldState} NewState={NewState} LauncherPid={LauncherPid} GamePid={GamePid} GameProcess={GameProcess} Reason={Reason}.", previous.State, state, nextLauncherPid?.ToString() ?? "unknown", nextGamePid?.ToString() ?? "unknown", nextGameProcess ?? "unknown", source);
        _logger.LogInformation("RUNTIME STATE UPDATE Source={Source} SessionId={SessionId} OldState={OldState} NewState={NewState}.", source, _sessionGeneration, previous.State, state);
    }

    private void AdvanceSessionGeneration()
    {
        lock (_stateGate)
        {
            _sessionGeneration++;
        }
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
