using GameShell.Core.Interfaces;
using GameShell.Core.Models;

namespace GameShell.Core.Services;

public sealed class ModeCommandService : IModeCommandService
{
    private readonly IModeOrchestrator _modeOrchestrator;

    public ModeCommandService(IModeOrchestrator modeOrchestrator)
    {
        _modeOrchestrator = modeOrchestrator;
    }

    public Task<AppStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return _modeOrchestrator.GetStatusAsync(cancellationToken);
    }

    public Task<ModeResult> EnterGameShellAsync(CancellationToken cancellationToken = default)
    {
        return _modeOrchestrator.EnterGameShellAsync(cancellationToken);
    }

    public Task<ModeResult> EnterGameShellWithDisplayProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        return _modeOrchestrator.EnterGameShellWithDisplayProfileAsync(profileName, cancellationToken);
    }

    public Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
    {
        return _modeOrchestrator.EnterWorkModeAsync(cancellationToken);
    }
}
