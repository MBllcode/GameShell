using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

public interface IDisplayRecoveryService
{
    Task<DisplayRecoveryAttempt> RecoverAsync(
        string profileName,
        ProfileDisplayTarget savedTarget,
        DisplayDeviceInfo resolvedTarget,
        IReadOnlyList<DisplayModeInfo> beforeModes,
        CancellationToken cancellationToken = default);
}