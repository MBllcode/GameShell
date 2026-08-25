using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Services;

/// <summary>
/// Reconciles the first stable Windows topology with the existing Work Mode
/// path when the persisted/session state says that no gaming session is active.
/// </summary>
public sealed class StartupReconciliationService
{
    private static readonly TimeSpan StartupConvergenceWindow = TimeSpan.FromSeconds(15);
    private const int StableSnapshotsRequired = 2;
    private const int MaximumRestoreAttempts = 2;

    private readonly IRuntimeStateService _runtimeStateService;
    private readonly IConsoleSessionManager _consoleSessionManager;
    private readonly IDisplayQueryService _displayQueryService;
    private readonly IDisplayDiscoveryStabilizationService _displayDiscoveryStabilizationService;
    private readonly ILogger<StartupReconciliationService> _logger;
    private readonly ITransientOperationStateService? _transientOperationStateService;

    public StartupReconciliationService(
        IRuntimeStateService runtimeStateService,
        IConsoleSessionManager consoleSessionManager,
        IDisplayQueryService displayQueryService,
        IDisplayDiscoveryStabilizationService displayDiscoveryStabilizationService,
        ILogger<StartupReconciliationService> logger,
        ITransientOperationStateService? transientOperationStateService = null)
    {
        _runtimeStateService = runtimeStateService;
        _consoleSessionManager = consoleSessionManager;
        _displayQueryService = displayQueryService;
        _displayDiscoveryStabilizationService = displayDiscoveryStabilizationService;
        _logger = logger;
        _transientOperationStateService = transientOperationStateService;
    }

    public async Task<ModeResult?> ReconcileAsync(
        AppSettings settings,
        bool explicitGamingStartup,
        CancellationToken cancellationToken = default)
    {
        var session = _consoleSessionManager.GetSnapshot();
        var runtime = await _runtimeStateService.LoadAsync(cancellationToken);

        if (explicitGamingStartup || IsRecoverableGamingState(runtime, session))
        {
            _logger.LogInformation(
                "[STARTUP RECONCILIATION] RuntimeMode={RuntimeMode} SessionState={SessionState} ActiveProfile={ActiveProfile} Result=PreserveGamingIntent",
                runtime.CurrentMode?.ToString() ?? "Work",
                session.State,
                session.ProfileName ?? "null");
            return null;
        }

        var expected = GetExpectedWorkLayouts(settings, runtime);
        if (expected.Count == 0)
        {
            _logger.LogInformation(
                "[STARTUP CONVERGENCE] RuntimeMode=Work SessionState={SessionState} ActiveProfile={ActiveProfile} Result=NoExpectedLayout",
                session.State,
                session.ProfileName ?? "null");
            return null;
        }

        var transientOperationId = _transientOperationStateService?.Begin(
            TransientOperationKind.Startup,
            TransientOperationPhase.PreparingDisplays);

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var stableSnapshots = 0;
            var restoreAttempts = 0;
            ModeResult? lastRestoreResult = null;

            for (var attempt = 1;
                 attempt <= StableSnapshotsRequired + MaximumRestoreAttempts + 2
                 && stopwatch.Elapsed < StartupConvergenceWindow;
                 attempt++)
            {
                var remaining = StartupConvergenceWindow - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                try
                {
                    // Ask the coordinator for its stable snapshot boundary. The
                    // coordinator also probes for a topology change when it is
                    // already Ready, so startup does not rely on the first raw
                    // post-boot inventory being authoritative.
                    await _displayDiscoveryStabilizationService
                        .GetStableDisplaysAsync(cancellationToken)
                        .WaitAsync(remaining, cancellationToken);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning(
                        "[STARTUP CONVERGENCE] Attempt={Attempt} Result=DiscoveryTimeout DurationMs={DurationMs}",
                        attempt,
                        stopwatch.ElapsedMilliseconds);
                    break;
                }

                _transientOperationStateService?.SetPhase(
                    transientOperationId ?? "-",
                    TransientOperationPhase.SynchronizingDisplays);
                var actual = await _displayQueryService.CaptureCurrentLayoutAsync(cancellationToken);
                var matches = expected.Any(layout => DisplayLayoutVerifier.MatchesSnapshot(layout, actual));
                stableSnapshots = matches ? stableSnapshots + 1 : 0;

                _logger.LogInformation(
                    "[STARTUP CONVERGENCE] Attempt={Attempt} RuntimeMode=Work SessionState={SessionState} ActiveProfile={ActiveProfile} ActualDisplays=[{ActualDisplays}] ExpectedDisplays=[{ExpectedDisplays}] StableSnapshots={StableSnapshots} Result={Result}",
                    attempt,
                    session.State,
                    session.ProfileName ?? "null",
                    FormatDisplays(actual),
                    FormatDisplays(expected),
                    stableSnapshots,
                    matches ? "Match" : "Mismatch");

                if (stableSnapshots >= StableSnapshotsRequired)
                {
                    _logger.LogInformation(
                        "[STARTUP CONVERGENCE] Result=Stable DurationMs={DurationMs}",
                        stopwatch.ElapsedMilliseconds);
                    return null;
                }

                if (matches || restoreAttempts >= MaximumRestoreAttempts)
                {
                    continue;
                }

                restoreAttempts++;
                _transientOperationStateService?.SetPhase(
                    transientOperationId ?? "-",
                    TransientOperationPhase.RestoringDisplays);
                _logger.LogInformation(
                    "[STARTUP CONVERGENCE] Action=RestoreWorkMode Attempt={Attempt} Result=Started",
                    restoreAttempts);
                lastRestoreResult = await _consoleSessionManager.EnterWorkModeAsync(cancellationToken);
                _transientOperationStateService?.SetPhase(
                    transientOperationId ?? "-",
                    TransientOperationPhase.SynchronizingDisplays);
                _logger.LogInformation(
                    "[STARTUP CONVERGENCE] Action=RestoreWorkMode Attempt={Attempt} Result={Result} Message={Message}",
                    restoreAttempts,
                    lastRestoreResult.Success ? "Success" : "Failed",
                    lastRestoreResult.Message);

                if (!lastRestoreResult.Success)
                {
                    return lastRestoreResult;
                }
            }

            _logger.LogWarning(
                "[STARTUP CONVERGENCE] Result=Timeout DurationMs={DurationMs} RestoreAttempts={RestoreAttempts} LastRestoreResult={LastRestoreResult}",
                stopwatch.ElapsedMilliseconds,
                restoreAttempts,
                lastRestoreResult?.Status.ToString() ?? "None");
            return new ModeResult
            {
                Status = ModeResultStatus.TimedOut,
                CurrentState = ModeState.Error,
                Message = "StartupConvergenceTimeout",
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        finally
        {
            if (transientOperationId is not null)
            {
                _transientOperationStateService?.Clear(transientOperationId, "CompletedOrFailed");
            }
        }
    }

    private static bool IsRecoverableGamingState(RuntimeState runtime, ConsoleSessionSnapshot session)
    {
        if (session.State is not ConsoleSessionState.Idle and not ConsoleSessionState.Faulted
            || !string.IsNullOrWhiteSpace(session.ProfileName))
        {
            return true;
        }

        if (runtime.CurrentMode is GameShellMode.Console or GameShellMode.GamingProfile)
        {
            return true;
        }

        return runtime.LastResult.StartsWith("Success", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(runtime.LastRequestedMode, GameShellMode.Console.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(runtime.LastRequestedMode, GameShellMode.GamingProfile.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<DisplayLayoutSnapshot> GetExpectedWorkLayouts(AppSettings settings, RuntimeState runtime)
    {
        return new[]
            {
                settings.WorkMode.DefaultDisplayLayout,
                runtime.WorkReturnLayout,
                runtime.PreviousDisplayLayout
            }
            .Where(layout => layout is not null)
            .Cast<DisplayLayoutSnapshot>()
            .DistinctBy(layout => layout.Id)
            .ToList();
    }

    private static string FormatDisplays(DisplayLayoutSnapshot layout)
    {
        return string.Join(", ", layout.Displays.Where(display => display.IsActive).Select(display => display.Identity.FriendlyName));
    }

    private static string FormatDisplays(IReadOnlyList<DisplayLayoutSnapshot> layouts)
    {
        return string.Join(" | ", layouts.Select(FormatDisplays));
    }
}
