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
        ILogger<ModeOrchestrator> logger)
    {
        _settingsService = settingsService;
        _modeWorkflowService = modeWorkflowService;
        _displayTransitionService = displayTransitionService;
        _runtimeStateService = runtimeStateService;
        _logger = logger;
    }

    public async Task<AppStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        var runtimeState = await _runtimeStateService.LoadAsync(cancellationToken);
        if (_currentMode is null && runtimeState.CurrentMode is not null)
        {
            _currentMode = runtimeState.CurrentMode;
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
        return RunPcGamingModeAsync(profileName, cancellationToken);
    }

    public Task<ModeResult> RunPcGamingModeAsync(CancellationToken cancellationToken = default)
    {
        return RunPcGamingModeAsync(null, cancellationToken);
    }

    private Task<ModeResult> RunPcGamingModeAsync(string? profileName, CancellationToken cancellationToken = default)
    {
        return RunModeAsync(
            operationName: "Enter PC Gaming",
            switchingState: ModeState.SwitchingToConsole,
            completedState: ModeState.GameShell,
            transition: settings => _displayTransitionService.EnterPcGamingModeAsync(
                ResolvePcGamingLayout(settings.Display, profileName),
                GetConsoleDisplays(settings),
                cancellationToken),
            preWorkflow: settings => _modeWorkflowService.PrepareGamingWorkflowAsync(
                BuildEffectivePcGamingSettings(settings, profileName),
                GameShellMode.PcGaming,
                cancellationToken),
            workflow: settings => _modeWorkflowService.CompleteGamingWorkflowAsync(
                BuildEffectivePcGamingSettings(settings, profileName),
                GameShellMode.PcGaming,
                cancellationToken),
            completionDelay: settings => TimeSpan.FromSeconds(ResolvePcGamingBehaviour(settings.Display, settings.PcGaming, profileName).CompletionDelaySeconds),
            runtimeMode: GameShellMode.PcGaming,
            cancellationToken);
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
        bool scriptFailureIsWarning = false)
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
        var activeProfileName = ResolvePcGamingProfileName(settings.Display, null);
        var activeConfiguration = ResolvePcGamingProfileConfiguration(settings.Display, activeProfileName);

        lock (_statusGate)
        {
            return new AppStatus
            {
                State = _state,
                CurrentMode = _currentMode?.ToString(),
                IsBusy = _state is ModeState.SwitchingToConsole or ModeState.SwitchingToWork,
                DisplayMode = "NativeDisplayConfig",
                SelectedConsoleDisplay = GetConsoleDisplays(settings).FirstOrDefault()?.FriendlyName,
                HasPcGamingLayout = ResolvePcGamingLayout(settings.Display, null) is not null,
                ActiveLauncher = activeConfiguration?.Launcher.Type ?? settings.Launcher.Type,
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

    private static DisplayLayoutSnapshot? ResolvePcGamingLayout(DisplaySettings display, string? profileName)
    {
        if (!string.IsNullOrWhiteSpace(profileName)
            && !string.Equals(profileName, "PcGaming", StringComparison.OrdinalIgnoreCase))
        {
            var namedProfile = display.PcGamingProfiles.FirstOrDefault(profile => string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase));
            if (namedProfile is not null)
            {
                return namedProfile.Layout;
            }
        }

        if (!string.IsNullOrWhiteSpace(display.ActivePcGamingProfileName))
        {
            var activeProfile = display.PcGamingProfiles.FirstOrDefault(profile => string.Equals(profile.Name, display.ActivePcGamingProfileName, StringComparison.OrdinalIgnoreCase));
            if (activeProfile is not null)
            {
                return activeProfile.Layout;
            }
        }

        return display.PcGamingLayout;
    }

    private static AppSettings BuildEffectivePcGamingSettings(AppSettings settings, string? profileName)
    {
        var resolvedProfileName = ResolvePcGamingProfileName(settings.Display, profileName);
        var profileConfiguration = ResolvePcGamingProfileConfiguration(settings.Display, resolvedProfileName);

        return new AppSettings
        {
            Display = settings.Display,
            Launcher = profileConfiguration?.Launcher ?? settings.Launcher,
            GameShell = settings.GameShell,
            PcGaming = profileConfiguration?.Behaviour ?? settings.PcGaming,
            WorkMode = settings.WorkMode,
            Audio = settings.Audio,
            Controller = settings.Controller,
            Hotkeys = settings.Hotkeys,
            Startup = settings.Startup,
            Ui = settings.Ui
        };
    }

    private static PcGamingSettings ResolvePcGamingBehaviour(DisplaySettings display, PcGamingSettings defaultBehaviour, string? profileName)
    {
        var resolvedProfileName = ResolvePcGamingProfileName(display, profileName);
        var configuration = ResolvePcGamingProfileConfiguration(display, resolvedProfileName);
        if (configuration is not null)
        {
            return configuration.Behaviour;
        }

        if (string.IsNullOrWhiteSpace(resolvedProfileName))
        {
            return defaultBehaviour;
        }

        var legacyBehaviour = display.PcGamingProfileBehaviours.FirstOrDefault(candidate => string.Equals(candidate.ProfileName, resolvedProfileName, StringComparison.OrdinalIgnoreCase));
        return legacyBehaviour?.Behaviour ?? defaultBehaviour;
    }

    private static PcGamingProfileConfiguration? ResolvePcGamingProfileConfiguration(DisplaySettings display, string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return null;
        }

        return display.PcGamingProfileConfigurations.FirstOrDefault(candidate => string.Equals(candidate.ProfileName, profileName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolvePcGamingProfileName(DisplaySettings display, string? requestedProfileName)
    {
        if (!string.IsNullOrWhiteSpace(requestedProfileName)
            && !string.Equals(requestedProfileName, "PcGaming", StringComparison.OrdinalIgnoreCase)
            && display.PcGamingProfiles.Any(profile => string.Equals(profile.Name, requestedProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            return requestedProfileName;
        }

        if (!string.IsNullOrWhiteSpace(display.ActivePcGamingProfileName)
            && display.PcGamingProfiles.Any(profile => string.Equals(profile.Name, display.ActivePcGamingProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            return display.ActivePcGamingProfileName;
        }

        return display.PcGamingProfiles.FirstOrDefault()?.Name;
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
