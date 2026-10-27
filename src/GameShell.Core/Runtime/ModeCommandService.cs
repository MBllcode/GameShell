using GameShell.Core.Displays;
using GameShell.Core.Profiles;

namespace GameShell.Core.Runtime;

public sealed class ModeCommandService : IModeCommandService
{
    private readonly IModeOrchestrator _modeOrchestrator;
    private readonly IProfileSessionManager _profileSessionManager;
    private readonly IDisplayDiscoveryStabilizationService? _displayDiscoveryStabilizationService;

    public ModeCommandService(
        IModeOrchestrator modeOrchestrator,
        IProfileSessionManager profileSessionManager,
        IDisplayDiscoveryStabilizationService? displayDiscoveryStabilizationService = null)
    {
        _modeOrchestrator = modeOrchestrator;
        _profileSessionManager = profileSessionManager;
        _displayDiscoveryStabilizationService = displayDiscoveryStabilizationService;
    }

    public Task<AppStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return GetComposedStatusAsync(cancellationToken);
    }

    private async Task<AppStatus> GetComposedStatusAsync(CancellationToken cancellationToken)
    {
        var status = await _modeOrchestrator.GetStatusAsync(cancellationToken);
        var session = _profileSessionManager.GetSnapshot();
        var transientOperation = status.TransientOperation;
        if (!transientOperation.IsActive
            && _displayDiscoveryStabilizationService?.State is DisplayDiscoveryState.Loading or DisplayDiscoveryState.Settling)
        {
            transientOperation = new TransientOperationState
            {
                Kind = TransientOperationKind.DisplayDiscovery,
                Phase = TransientOperationPhase.SynchronizingDisplays,
                MessageKey = "status.transient.synchronizingDisplays",
                StartedAt = DateTimeOffset.Now,
                TransitionId = "display-discovery"
            };
        }

        return new AppStatus
        {
            State = status.State,
            CurrentMode = status.CurrentMode,
            SessionState = session.State.ToString(),
            ActiveProfileName = session.ProfileName,
            IsBusy = status.IsBusy,
            DisplayMode = status.DisplayMode,
            SelectedConsoleDisplay = status.SelectedConsoleDisplay,
            HasGamingProfiles = status.HasGamingProfiles,
            ActiveLauncher = string.Equals(status.CurrentMode, GameShellMode.Work.ToString(), StringComparison.Ordinal) ? null : status.ActiveLauncher,
            LastOperation = status.LastOperation,
            LastOperationDurationMs = status.LastOperationDurationMs,
            LastError = status.LastError,
            TransientOperation = transientOperation,
            SettingsPath = status.SettingsPath
        };
    }

    public Task<ModeResult> EnterGameShellAsync(CancellationToken cancellationToken = default)
    {
        return _profileSessionManager.EnterSelectedProfileAsync(cancellationToken);
    }

    public Task<ModeResult> EnterGameShellWithDisplayProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        return _profileSessionManager.EnterProfileAsync(profileName, cancellationToken);
    }

    public Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
    {
        return _profileSessionManager.EnterWorkModeAsync(cancellationToken);
    }
}
