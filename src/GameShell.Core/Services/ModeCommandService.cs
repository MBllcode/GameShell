using GameShell.Core.Interfaces;
using GameShell.Core.Models;

namespace GameShell.Core.Services;

public sealed class ModeCommandService : IModeCommandService
{
    private readonly IModeOrchestrator _modeOrchestrator;
    private readonly IConsoleSessionManager _consoleSessionManager;

    public ModeCommandService(
        IModeOrchestrator modeOrchestrator,
        IConsoleSessionManager consoleSessionManager)
    {
        _modeOrchestrator = modeOrchestrator;
        _consoleSessionManager = consoleSessionManager;
    }

    public Task<AppStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return GetComposedStatusAsync(cancellationToken);
    }

    private async Task<AppStatus> GetComposedStatusAsync(CancellationToken cancellationToken)
    {
        var status = await _modeOrchestrator.GetStatusAsync(cancellationToken);
        var session = _consoleSessionManager.GetSnapshot();
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
            SettingsPath = status.SettingsPath
        };
    }

    public Task<ModeResult> EnterGameShellAsync(CancellationToken cancellationToken = default)
    {
        return _consoleSessionManager.EnterConsoleAsync(cancellationToken);
    }

    public Task<ModeResult> EnterGameShellWithDisplayProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        return _consoleSessionManager.EnterProfileAsync(profileName, cancellationToken);
    }

    public Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
    {
        return _consoleSessionManager.EnterWorkModeAsync(cancellationToken);
    }
}
