using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

public interface IDisplayQueryService
{
    Task<IReadOnlyList<DisplayDeviceInfo>> GetAvailableDisplaysAsync(
        CancellationToken cancellationToken = default);

    Task<DisplayLayoutSnapshot> CaptureCurrentLayoutAsync(
        CancellationToken cancellationToken = default);
}

public interface IDisplayConfigurationService : IDisplayQueryService, IDisplayLayoutApplicationService
{
}

public interface IDisplayLayoutApplicationService
{
    Task<DisplayOperationResult> ValidateLayoutAsync(
        DisplayLayoutSnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task<DisplayOperationResult> ApplyLayoutAsync(
        DisplayLayoutSnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task<DisplayOperationResult> ActivateOnlyAsync(
        DisplayIdentity display,
        CancellationToken cancellationToken = default);

    Task<DisplayOperationResult> ActivateOnlyAsync(
        IReadOnlyCollection<DisplayIdentity> displays,
        CancellationToken cancellationToken = default)
    {
        var display = displays.FirstOrDefault();
        return display is null
            ? Task.FromResult(new DisplayOperationResult { Success = false, Message = "Nie wybrano ekranów Console Mode." })
            : ActivateOnlyAsync(display, cancellationToken);
    }
}

public interface IDisplayTransitionService
{
    Task<DisplayTransitionResult> EnterConsoleModeAsync(
        DisplayIdentity? consoleDisplay,
        CancellationToken cancellationToken = default);

    Task<DisplayTransitionResult> EnterConsoleModeAsync(
        IReadOnlyCollection<DisplayIdentity>? consoleDisplays,
        CancellationToken cancellationToken = default)
    {
        return EnterConsoleModeAsync(consoleDisplays?.FirstOrDefault(), cancellationToken);
    }

    Task<DisplayTransitionResult> EnterPcGamingModeAsync(
        DisplayLayoutSnapshot? pcGamingLayout,
        DisplayIdentity? fallbackConsoleDisplay,
        CancellationToken cancellationToken = default);

    Task<DisplayTransitionResult> EnterPcGamingModeAsync(
        DisplayLayoutSnapshot? pcGamingLayout,
        IReadOnlyCollection<DisplayIdentity>? fallbackConsoleDisplays,
        CancellationToken cancellationToken = default)
    {
        return EnterPcGamingModeAsync(pcGamingLayout, fallbackConsoleDisplays?.FirstOrDefault(), cancellationToken);
    }

    Task<DisplayTransitionResult> EnterWorkModeAsync(
        CancellationToken cancellationToken = default);

    Task<DisplayTransitionResult> TestConsoleDisplayAsync(
        DisplayIdentity? consoleDisplay,
        CancellationToken cancellationToken = default);

    Task<DisplayTransitionResult> TestConsoleDisplayAsync(
        IReadOnlyCollection<DisplayIdentity>? consoleDisplays,
        CancellationToken cancellationToken = default)
    {
        return TestConsoleDisplayAsync(consoleDisplays?.FirstOrDefault(), cancellationToken);
    }

    Task<DisplayTransitionResult> TestPcGamingLayoutAsync(
        DisplayLayoutSnapshot? pcGamingLayout,
        CancellationToken cancellationToken = default);

    Task<DisplayTransitionResult> ConfirmPendingTestAsync(
        CancellationToken cancellationToken = default);

    Task<DisplayTransitionResult> RestorePendingTestAsync(
        CancellationToken cancellationToken = default);

    Task<DisplayTransitionResult> RestorePreviousLayoutAsync(
        CancellationToken cancellationToken = default);

    Task<RuntimeState> LoadRuntimeStateAsync(
        CancellationToken cancellationToken = default);
}
