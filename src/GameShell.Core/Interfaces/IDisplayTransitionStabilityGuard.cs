using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

public interface IDisplayTransitionStabilityGuard
{
    Task<DisplayTransitionResult?> VerifyBeforeLauncherAsync(
        DisplayLayoutSnapshot? expectedTargetLayout,
        string? profileName,
        CancellationToken cancellationToken = default);
}
