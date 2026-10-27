
namespace GameShell.Core.Displays;

public interface IDisplayTransitionStabilityGuard
{
    Task<DisplayTransitionResult?> VerifyBeforeLauncherAsync(
        DisplayLayoutSnapshot? expectedTargetLayout,
        string? profileName,
        CancellationToken cancellationToken = default);
}
