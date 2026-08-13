using System.Diagnostics;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Services;

public sealed class ModeOrchestrator : IModeOrchestrator
{
    private readonly ISettingsService _settingsService;
    private readonly IModeWorkflowService _modeWorkflowService;
    private readonly IDisplayTransitionService _displayTransitionService;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly GamingProfileRuntimeSettingsResolver _gamingProfileSettingsResolver;
    private readonly GamingProfileReadinessCoordinator _gamingProfileReadinessCoordinator;
    private readonly ILogger<ModeOrchestrator> _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _statusGate = new();

    private ModeState _state = ModeState.Unknown;
    private GameShellMode? _currentMode;
    private string? _lastOperation;
    private long? _lastOperationDurationMs;
    private string? _lastError;

    public ModeOrchestrator(
        ISettingsService settingsService,
        IModeWorkflowService modeWorkflowService,
        IDisplayTransitionService displayTransitionService,
        IRuntimeStateService runtimeStateService,
        IGamingProfileTopologyReadinessEvaluator? topologyReadinessEvaluator,
        IDisplayRecoveryService? displayRecoveryService,
        ILogger<ModeOrchestrator> logger,
        GamingProfileRuntimeSettingsResolver? gamingProfileSettingsResolver = null)
    {
        _settingsService = settingsService;
        _modeWorkflowService = modeWorkflowService;
        _displayTransitionService = displayTransitionService;
        _runtimeStateService = runtimeStateService;
        _gamingProfileSettingsResolver = gamingProfileSettingsResolver ?? new GamingProfileRuntimeSettingsResolver();
        _gamingProfileReadinessCoordinator = new GamingProfileReadinessCoordinator(
            settingsService,
            _gamingProfileSettingsResolver,
            topologyReadinessEvaluator,
            displayRecoveryService,
            logger);
        _logger = logger;
    }

    public ModeOrchestrator(
        ISettingsService settingsService,
        IModeWorkflowService modeWorkflowService,
        IDisplayTransitionService displayTransitionService,
        IRuntimeStateService runtimeStateService,
        ILogger<ModeOrchestrator> logger)
        : this(settingsService, modeWorkflowService, displayTransitionService, runtimeStateService, null, null, logger)
    {
    }

    public ModeOrchestrator(
        ISettingsService settingsService,
        IModeWorkflowService modeWorkflowService,
        IDisplayTransitionService displayTransitionService,
        IRuntimeStateService runtimeStateService,
        IGamingProfileTopologyReadinessEvaluator topologyReadinessEvaluator,
        ILogger<ModeOrchestrator> logger)
        : this(settingsService, modeWorkflowService, displayTransitionService, runtimeStateService, topologyReadinessEvaluator, null, logger)
    {
    }

    public async Task<AppStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        var runtimeState = await _runtimeStateService.LoadAsync(cancellationToken);
        if (_currentMode is null && runtimeState.CurrentMode is not null)
        {
            _currentMode = runtimeState.CurrentMode;
            _state = runtimeState.CurrentMode == GameShellMode.Work
                ? ModeState.WorkMode
                : ModeState.GameShell;
        }
        else if (_currentMode is null
            && runtimeState.CurrentMode is null
            && _state == ModeState.Unknown
            && settings.Ui.IsFirstRunCompleted
            && string.Equals(runtimeState.LastResult, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            _currentMode = GameShellMode.Work;
            _state = ModeState.WorkMode;
            runtimeState.CurrentMode = GameShellMode.Work;
            runtimeState.LastRequestedMode = GameShellMode.Work.ToString();
            runtimeState.LastCompletedAt = DateTimeOffset.Now;
            runtimeState.LastResult = "Initialized";
            await _runtimeStateService.SaveAsync(runtimeState, cancellationToken);
            _logger.LogInformation("Runtime mode initialized to Work after completed first run.");
        }

        return CreateStatus(settings);
    }

    public Task<ModeResult> EnterGameShellAsync(CancellationToken cancellationToken = default)
    {
        return RunModeAsync(
            operationName: "Enter Console Mode",
            switchingState: ModeState.SwitchingToConsole,
            completedState: ModeState.GameShell,
            transition: settings => _displayTransitionService.EnterConsoleModeAsync(GetConsoleDisplays(settings), cancellationToken),
            preWorkflow: settings => _modeWorkflowService.PrepareGamingWorkflowAsync(settings, GameShellMode.Console, cancellationToken),
            workflow: settings => _modeWorkflowService.CompleteGamingWorkflowAsync(settings, GameShellMode.Console, cancellationToken),
            completionDelay: settings => TimeSpan.FromSeconds(settings.GameShell.CompletionDelaySeconds),
            runtimeMode: GameShellMode.Console,
            cancellationToken);
    }

    public Task<ModeResult> EnterGameShellWithDisplayProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        return RunGamingModeAsync(profileName, cancellationToken);
    }

    public Task<ModeResult> RunGamingModeAsync(CancellationToken cancellationToken = default)
    {
        return RunGamingModeAsync(null, cancellationToken);
    }

    private Task<ModeResult> RunGamingModeAsync(string? profileName, CancellationToken cancellationToken = default)
    {
        return RunModeAsync(
            operationName: "Enter Gaming Profile",
            switchingState: ModeState.SwitchingToConsole,
            completedState: ModeState.GameShell,
            transition: settings => _displayTransitionService.EnterGamingModeAsync(
                _gamingProfileSettingsResolver.ResolveLayout(settings.Display, profileName),
                GetConsoleDisplays(settings),
                cancellationToken),
            preWorkflow: settings => _modeWorkflowService.PrepareGamingWorkflowAsync(
                _gamingProfileSettingsResolver.BuildEffectiveSettings(settings, profileName),
                GameShellMode.GamingProfile,
                cancellationToken),
            workflow: settings => _modeWorkflowService.CompleteGamingWorkflowAsync(
                _gamingProfileSettingsResolver.BuildEffectiveSettings(settings, profileName),
                GameShellMode.GamingProfile,
                cancellationToken),
            completionDelay: settings => TimeSpan.FromSeconds(_gamingProfileSettingsResolver
                .ResolveBehaviour(settings.Display, settings.Gaming, profileName).CompletionDelaySeconds),
            runtimeMode: GameShellMode.GamingProfile,
            cancellationToken,
            preTransitionGate: settings => _gamingProfileReadinessCoordinator.CheckBeforeTransitionAsync(settings, profileName, cancellationToken),
            postTransitionGate: settings => _gamingProfileReadinessCoordinator.CheckAfterTransitionAsync(settings, profileName, cancellationToken));
    }

    public Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
    {
        return RunModeAsync(
            operationName: "Enter Work Mode",
            switchingState: ModeState.SwitchingToWork,
            completedState: ModeState.WorkMode,
            transition: _ => _displayTransitionService.EnterWorkModeAsync(cancellationToken),
            preWorkflow: settings => _modeWorkflowService.PrepareWorkWorkflowAsync(settings, cancellationToken),
            workflow: settings => _modeWorkflowService.RunWorkWorkflowAsync(settings, cancellationToken),
            completionDelay: _ => TimeSpan.Zero,
            runtimeMode: GameShellMode.Work,
            cancellationToken,
            scriptFailureIsWarning: true);
    }

    private async Task<ModeResult> RunModeAsync(
        string operationName,
        ModeState switchingState,
        ModeState completedState,
        Func<AppSettings, Task<DisplayTransitionResult>> transition,
        Func<AppSettings, Task<ModeWorkflowResult>>? preWorkflow,
        Func<AppSettings, Task<ModeWorkflowResult>> workflow,
        Func<AppSettings, TimeSpan> completionDelay,
        GameShellMode runtimeMode,
        CancellationToken cancellationToken,
        bool scriptFailureIsWarning = false,
        Func<AppSettings, Task<ModeResult?>>? preTransitionGate = null,
        Func<AppSettings, Task<ModeResult?>>? postTransitionGate = null)
    {
        if (!await _operationLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("{OperationName} rejected because another operation is running.", operationName);
            return new ModeResult
            {
                Status = ModeResultStatus.AlreadyRunning,
                CurrentState = GetState(),
                Message = "Inna operacja przełączania już trwa."
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var displayTransitionSucceeded = false;
        var transitionId = WorkModeTransitionContext.CurrentId ?? "-";

        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);
            SetStatus(switchingState, operationName, null, null);
            _logger.LogInformation("{OperationName} [{TransitionId}] started with native DisplayConfig workflow. CurrentState={CurrentState}.", operationName, transitionId, GetState());

            if (preTransitionGate is not null)
            {
                var gateResult = await preTransitionGate(settings);
                if (gateResult is not null)
                {
                    stopwatch.Stop();
                    SetStatus(ModeState.Error, operationName, stopwatch.ElapsedMilliseconds, gateResult.Message);
                    await SaveRuntimeResultAsync(runtimeMode, "ReadinessBlocked", cancellationToken);
                    return new ModeResult
                    {
                        Status = gateResult.Status,
                        CurrentState = gateResult.CurrentState,
                        Message = gateResult.Message,
                        DurationMs = stopwatch.ElapsedMilliseconds
                    };
                }
            }

            if (preWorkflow is not null)
            {
                _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 2 START pre-display workflow.", transitionId);
                var preWorkflowResult = await preWorkflow(settings);
                if (!preWorkflowResult.Success)
                {
                    stopwatch.Stop();
                    var message = preWorkflowResult.Message ?? $"{operationName} pre-workflow failed.";
                    _logger.LogError("WORK MODE TRANSITION FAILED [Step=pre-display workflow, Message={Message}].", message);
                    SetStatus(ModeState.Error, operationName, stopwatch.ElapsedMilliseconds, message);
                    await SaveRuntimeResultAsync(runtimeMode, "Failed", cancellationToken);

                    return new ModeResult
                    {
                        Status = ModeResultStatus.Failed,
                        CurrentState = ModeState.Error,
                        Message = message,
                        DurationMs = stopwatch.ElapsedMilliseconds
                    };
                }

                _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 2 SUCCESS pre-display workflow.", transitionId);
            }

            _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 3 START display transition.", transitionId);
            var transitionResult = await transition(settings);
            if (!transitionResult.Success)
            {
                stopwatch.Stop();
                var message = transitionResult.UserMessage ?? "Nie udało się przełączyć ekranów.";
                _logger.LogError("WORK MODE TRANSITION FAILED [Step=display transition, ErrorCode={ErrorCode}, Message={Message}, CurrentStateAfterFailure={CurrentStateAfterFailure}].", transitionResult.ErrorCode, message, GetState());
                SetStatus(ModeState.Error, operationName, stopwatch.ElapsedMilliseconds, message);
                await SaveRuntimeResultAsync(runtimeMode, "DisplayFailed", cancellationToken);

                return new ModeResult
                {
                    Status = transitionResult.ErrorCode is "ConsoleDisplayMissing" ? ModeResultStatus.InvalidConfiguration : ModeResultStatus.Failed,
                    CurrentState = ModeState.Error,
                    Message = message,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            if (postTransitionGate is not null)
            {
                var postTransitionResult = await postTransitionGate(settings);
                if (postTransitionResult is not null)
                {
                    var rollback = await _displayTransitionService.RestorePreviousLayoutAsync(cancellationToken);
                    _logger.LogWarning(
                        "WORK MODE TRANSITION [{TransitionId}] postcheck failed. RollbackSuccess={RollbackSuccess}.",
                        transitionId,
                        rollback.Success);
                    stopwatch.Stop();
                    SetStatus(ModeState.Error, operationName, stopwatch.ElapsedMilliseconds, postTransitionResult.Message);
                    await SaveRuntimeResultAsync(runtimeMode, "PostcheckBlocked", cancellationToken);
                    return new ModeResult
                    {
                        Status = postTransitionResult.Status,
                        CurrentState = postTransitionResult.CurrentState,
                        Message = postTransitionResult.Message,
                        DurationMs = stopwatch.ElapsedMilliseconds
                    };
                }
            }

            displayTransitionSucceeded = true;
            _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 3 SUCCESS display transition.", transitionId);

            _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 4 START post-display workflow.", transitionId);
            var workflowResult = await workflow(settings);
            if (!workflowResult.Success)
            {
                stopwatch.Stop();
                var message = workflowResult.Message ?? $"{operationName} workflow failed.";
                _logger.LogError("WORK MODE TRANSITION FAILED [Step=post-display workflow, Message={Message}].", message);
                if (scriptFailureIsWarning)
                {
                    SetStatus(completedState, operationName, stopwatch.ElapsedMilliseconds, message, runtimeMode);
                    await SaveRuntimeResultAsync(runtimeMode, "SuccessWithScriptWarning", cancellationToken);

                    return new ModeResult
                    {
                        Status = ModeResultStatus.Success,
                        CurrentState = completedState,
                        Message = "Ekrany zostaly przelaczone. Dodatkowy krok Work Mode nie zakonczyl sie poprawnie.",
                        DurationMs = stopwatch.ElapsedMilliseconds
                    };
                }

                SetStatus(ModeState.Error, operationName, stopwatch.ElapsedMilliseconds, message);
                await SaveRuntimeResultAsync(runtimeMode, "Failed", cancellationToken);

                return new ModeResult
                {
                    Status = ModeResultStatus.Failed,
                    CurrentState = ModeState.Error,
                    Message = message,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            _logger.LogInformation("WORK MODE TRANSITION [{TransitionId}] STEP 4 SUCCESS post-display workflow.", transitionId);

            var delay = completionDelay(settings);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            stopwatch.Stop();
            SetStatus(completedState, operationName, stopwatch.ElapsedMilliseconds, null, runtimeMode);
            await SaveRuntimeResultAsync(runtimeMode, "Success", cancellationToken);
            _logger.LogInformation("{OperationName} [{TransitionId}] completed in {DurationMs} ms. CurrentState={CurrentState}.", operationName, transitionId, stopwatch.ElapsedMilliseconds, GetState());

            return new ModeResult
            {
                Status = ModeResultStatus.Success,
                CurrentState = completedState,
                Message = transitionResult.UserMessage ?? $"{operationName} completed.",
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException exception)
        {
            stopwatch.Stop();
            SetStatus(ModeState.Unknown, operationName, stopwatch.ElapsedMilliseconds, "Operation cancelled.");
            await SaveRuntimeResultAsync(runtimeMode, "Cancelled", CancellationToken.None);
            _logger.LogWarning(
                "{OperationName} CANCELLATION DETECTED [TransitionId={TransitionId}, Operation={OperationName}, ExceptionType={ExceptionType}, CancellationTokenIsCancellationRequested={CancellationTokenIsCancellationRequested}, ExternalTokenIsCancellationRequested={ExternalTokenIsCancellationRequested}, TimeoutTokenIsCancellationRequested={TimeoutTokenIsCancellationRequested}, ElapsedMs={ElapsedMs}].",
                operationName,
                transitionId,
                operationName,
                exception.GetType().FullName,
                exception.CancellationToken.IsCancellationRequested,
                cancellationToken.IsCancellationRequested,
                false,
                stopwatch.ElapsedMilliseconds);

            return new ModeResult
            {
                Status = ModeResultStatus.Cancelled,
                CurrentState = ModeState.Unknown,
                Message = "Operacja została anulowana.",
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (scriptFailureIsWarning && displayTransitionSucceeded)
            {
                SetStatus(completedState, operationName, stopwatch.ElapsedMilliseconds, ex.Message, runtimeMode);
                await SaveRuntimeResultAsync(runtimeMode, "SuccessWithScriptWarning", CancellationToken.None);
                _logger.LogWarning(ex, "WORK MODE TRANSITION [{TransitionId}] completed display transition but a later workflow step failed. ExceptionType={ExceptionType}, Message={Message}, InnerException={InnerException}.", transitionId, ex.GetType().FullName, ex.Message, ex.InnerException?.ToString() ?? "(none)");

                return new ModeResult
                {
                    Status = ModeResultStatus.Success,
                    CurrentState = completedState,
                    Message = "Ekrany zostaly przelaczone. Dodatkowy krok Work Mode nie zakonczyl sie poprawnie.",
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }

            SetStatus(ModeState.Error, operationName, stopwatch.ElapsedMilliseconds, ex.Message);
            await SaveRuntimeResultAsync(runtimeMode, "Failed", CancellationToken.None);
            _logger.LogError(ex, "WORK MODE TRANSITION FAILED [{TransitionId}] [Step=orchestrator, ExceptionType={ExceptionType}, Message={Message}, StackTrace={StackTrace}, InnerException={InnerException}, CurrentStateAfterFailure={CurrentStateAfterFailure}].", transitionId, ex.GetType().FullName, ex.Message, ex.StackTrace ?? "(none)", ex.InnerException?.ToString() ?? "(none)", GetState());

            return new ModeResult
            {
                Status = ModeResultStatus.Failed,
                CurrentState = ModeState.Error,
                Message = ex.Message,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task SaveRuntimeResultAsync(GameShellMode requestedMode, string result, CancellationToken cancellationToken)
    {
        var state = await _runtimeStateService.LoadAsync(cancellationToken);
        state.LastRequestedMode = requestedMode.ToString();
        state.LastCompletedAt = DateTimeOffset.Now;
        state.LastResult = result;
        if (result.StartsWith("Success", StringComparison.Ordinal))
        {
            state.CurrentMode = requestedMode;
        }

        await _runtimeStateService.SaveAsync(state, cancellationToken);
    }

    private AppStatus CreateStatus(AppSettings settings)
    {
        var activeProfileName = _gamingProfileSettingsResolver.ResolveProfileName(settings.Display, null);
        var activeConfiguration = _gamingProfileSettingsResolver.ResolveConfiguration(settings.Display, activeProfileName);

        lock (_statusGate)
        {
            return new AppStatus
            {
                State = _state,
                CurrentMode = _currentMode?.ToString(),
                IsBusy = _state is ModeState.SwitchingToConsole or ModeState.SwitchingToWork,
                DisplayMode = "NativeDisplayConfig",
                SelectedConsoleDisplay = GetConsoleDisplays(settings).FirstOrDefault()?.FriendlyName,
                HasGamingProfiles = _gamingProfileSettingsResolver.ResolveLayout(settings.Display, null) is not null,
                ActiveLauncher = _currentMode == GameShellMode.Work
                    ? null
                    : activeConfiguration?.Launcher.Type ?? settings.Launcher.Type,
                LastOperation = _lastOperation,
                LastOperationDurationMs = _lastOperationDurationMs,
                LastError = _lastError,
                SettingsPath = _settingsService.SettingsPath
            };
        }
    }

    private static IReadOnlyCollection<DisplayIdentity> GetConsoleDisplays(AppSettings settings)
    {
        if (settings.Display.ConsoleDisplays.Count > 0)
        {
            return settings.Display.ConsoleDisplays;
        }

        return settings.Display.ConsoleDisplay is null
            ? []
            : [settings.Display.ConsoleDisplay];
    }

    private ModeState GetState()
    {
        lock (_statusGate)
        {
            return _state;
        }
    }

    private void SetStatus(ModeState state, string? lastOperation, long? durationMs, string? error, GameShellMode? currentMode = null)
    {
        lock (_statusGate)
        {
            _state = state;
            if (currentMode is not null)
            {
                _currentMode = currentMode;
            }

            _lastOperation = lastOperation;
            _lastOperationDurationMs = durationMs;
            _lastError = error;
        }
    }
}
