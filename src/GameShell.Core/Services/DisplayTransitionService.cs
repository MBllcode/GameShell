using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Services;

public sealed class DisplayTransitionService : IDisplayTransitionService
{
    private const int VerificationAttempts = 6;
    private static readonly TimeSpan VerificationDelay = TimeSpan.FromMilliseconds(750);

    private readonly IDisplayQueryService _queryService;
    private readonly IDisplayLayoutApplicationService _layoutService;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly ConsoleDisplayLayoutBuilder _consoleLayoutBuilder;
    private readonly ILogger<DisplayTransitionService> _logger;

    private DisplayLayoutSnapshot? _pendingTestRollbackLayout;

    public DisplayTransitionService(
        IDisplayQueryService queryService,
        IDisplayLayoutApplicationService layoutService,
        IRuntimeStateService runtimeStateService,
        ConsoleDisplayLayoutBuilder consoleLayoutBuilder,
        ILogger<DisplayTransitionService> logger)
    {
        _queryService = queryService;
        _layoutService = layoutService;
        _runtimeStateService = runtimeStateService;
        _consoleLayoutBuilder = consoleLayoutBuilder;
        _logger = logger;
    }

    public Task<RuntimeState> LoadRuntimeStateAsync(CancellationToken cancellationToken = default)
    {
        return _runtimeStateService.LoadAsync(cancellationToken);
    }

    public Task<DisplayTransitionResult> EnterConsoleModeAsync(DisplayIdentity? consoleDisplay, CancellationToken cancellationToken = default)
    {
        return EnterConsoleModeAsync(consoleDisplay is null ? null : [consoleDisplay], cancellationToken);
    }

    public async Task<DisplayTransitionResult> EnterConsoleModeAsync(IReadOnlyCollection<DisplayIdentity>? consoleDisplays, CancellationToken cancellationToken = default)
    {
        if (consoleDisplays is null || consoleDisplays.Count == 0)
        {
            return DisplayTransitionResult.Fail("ConsoleDisplayMissing", "Najpierw wybierz ekran Console Mode w ustawieniach.");
        }

        var state = await _runtimeStateService.LoadAsync(cancellationToken);
        var current = await _queryService.CaptureCurrentLayoutAsync(cancellationToken);
        if (DisplayLayoutVerifier.IsConsoleLayout(current, consoleDisplays))
        {
            state.CurrentMode = GameShellMode.Console;
            EnsureWorkReturnLayout(state, current);
            await _runtimeStateService.SaveAsync(state, cancellationToken);
            return DisplayTransitionResult.Ok();
        }

        state.PreviousDisplayLayout = current;
        EnsureWorkReturnLayout(state, current);
        await _runtimeStateService.SaveAsync(state, cancellationToken);

        var apply = await _layoutService.ActivateOnlyAsync(consoleDisplays, cancellationToken);
        if (!apply.Success)
        {
            return await RollBackAsync(state, "ApplyFailed", apply.Message, cancellationToken);
        }

        if (!await WaitForLayoutAsync(
            actual => DisplayLayoutVerifier.IsConsoleLayout(actual, consoleDisplays),
            "Console",
            cancellationToken))
        {
            return await RollBackAsync(state, "VerificationFailed", "Nie udalo sie potwierdzic docelowego ukladu ekranow.", cancellationToken);
        }

        state.CurrentMode = GameShellMode.Console;
        await _runtimeStateService.SaveAsync(state, cancellationToken);
        return DisplayTransitionResult.Ok();
    }

    public async Task<DisplayTransitionResult> EnterPcGamingModeAsync(
        DisplayLayoutSnapshot? pcGamingLayout,
        DisplayIdentity? fallbackConsoleDisplay,
        CancellationToken cancellationToken = default)
    {
        return await EnterPcGamingModeAsync(
            pcGamingLayout,
            fallbackConsoleDisplay is null ? null : [fallbackConsoleDisplay],
            cancellationToken);
    }

    public async Task<DisplayTransitionResult> EnterPcGamingModeAsync(
        DisplayLayoutSnapshot? pcGamingLayout,
        IReadOnlyCollection<DisplayIdentity>? fallbackConsoleDisplays,
        CancellationToken cancellationToken = default)
    {
        if (pcGamingLayout is null)
        {
            return await EnterConsoleModeAsync(fallbackConsoleDisplays, cancellationToken);
        }

        var activeDisplays = pcGamingLayout.Displays.Where(display => display.IsActive).ToList();
        if (activeDisplays.Count == 1)
        {
            return await EnterSingleDisplayGamingModeAsync(activeDisplays[0].Identity, cancellationToken);
        }

        return await RunModeTransitionAsync(
            GameShellMode.PcGaming,
            _ => Task.FromResult(pcGamingLayout),
            actual => DisplayLayoutVerifier.MatchesSnapshot(pcGamingLayout, actual),
            cancellationToken);
    }

    private async Task<DisplayTransitionResult> EnterSingleDisplayGamingModeAsync(
        DisplayIdentity targetDisplay,
        CancellationToken cancellationToken)
    {
        var state = await _runtimeStateService.LoadAsync(cancellationToken);
        var current = await _queryService.CaptureCurrentLayoutAsync(cancellationToken);

        if (DisplayLayoutVerifier.IsConsoleLayout(current, [targetDisplay]))
        {
            state.CurrentMode = GameShellMode.PcGaming;
            EnsureWorkReturnLayout(state, current);
            await _runtimeStateService.SaveAsync(state, cancellationToken);
            return DisplayTransitionResult.Ok();
        }

        state.PreviousDisplayLayout = current;
        EnsureWorkReturnLayout(state, current);
        await _runtimeStateService.SaveAsync(state, cancellationToken);

        var apply = await _layoutService.ActivateOnlyAsync([targetDisplay], cancellationToken);
        if (!apply.Success)
        {
            return await RollBackAsync(state, "ApplyFailed", apply.Message, cancellationToken);
        }

        if (!await WaitForLayoutAsync(actual => DisplayLayoutVerifier.IsConsoleLayout(actual, [targetDisplay]), "PcGamingSingle", cancellationToken))
        {
            return await RollBackAsync(state, "VerificationFailed", "Nie udało się potwierdzić docelowego układu ekranów.", cancellationToken);
        }

        state.CurrentMode = GameShellMode.PcGaming;
        await _runtimeStateService.SaveAsync(state, cancellationToken);
        return DisplayTransitionResult.Ok();
    }

    public async Task<DisplayTransitionResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
    {
        var state = await _runtimeStateService.LoadAsync(cancellationToken);
        var candidates = new[] { state.WorkReturnLayout, state.PreviousDisplayLayout }
            .Where(layout => layout is not null)
            .Cast<DisplayLayoutSnapshot>()
            .DistinctBy(layout => layout.Id)
            .ToList();
        if (candidates.Count == 0)
        {
            _logger.LogWarning("Work Mode requested without a WorkReturnLayout. Display layout will not be changed.");
            state.CurrentMode = GameShellMode.Work;
            await _runtimeStateService.SaveAsync(state, cancellationToken);
            return DisplayTransitionResult.Ok("Brak zapisanego układu Work. Pozostawiono obecny układ ekranów.");
        }

        return await RestoreWorkLayoutAsync(state, candidates, cancellationToken);
    }

    public async Task<DisplayTransitionResult> TestConsoleDisplayAsync(IReadOnlyCollection<DisplayIdentity>? consoleDisplays, CancellationToken cancellationToken = default)
    {
        if (consoleDisplays is null || consoleDisplays.Count == 0)
        {
            return DisplayTransitionResult.Fail("ConsoleDisplayMissing", "Najpierw wybierz ekran Console Mode w ustawieniach.");
        }

        var current = await _queryService.CaptureCurrentLayoutAsync(cancellationToken);
        return await StartConsoleTestAsync(current, consoleDisplays, cancellationToken);
    }

    public Task<DisplayTransitionResult> TestConsoleDisplayAsync(DisplayIdentity? consoleDisplay, CancellationToken cancellationToken = default)
    {
        return TestConsoleDisplayAsync(consoleDisplay is null ? null : [consoleDisplay], cancellationToken);
    }

    public async Task<DisplayTransitionResult> TestPcGamingLayoutAsync(DisplayLayoutSnapshot? pcGamingLayout, CancellationToken cancellationToken = default)
    {
        if (pcGamingLayout is null)
        {
            return DisplayTransitionResult.Fail("PcGamingLayoutMissing", "Najpierw zapisz układ PC Gaming.");
        }

        var current = await _queryService.CaptureCurrentLayoutAsync(cancellationToken);
        return await StartTestAsync(current, pcGamingLayout, actual => DisplayLayoutVerifier.MatchesSnapshot(pcGamingLayout, actual), cancellationToken);
    }

    public Task<DisplayTransitionResult> ConfirmPendingTestAsync(CancellationToken cancellationToken = default)
    {
        _pendingTestRollbackLayout = null;
        return Task.FromResult(DisplayTransitionResult.Ok("Układ ekranów zachowany."));
    }

    public async Task<DisplayTransitionResult> RestorePendingTestAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingTestRollbackLayout is null)
        {
            return DisplayTransitionResult.Fail("NoPendingTest", "Nie ma aktywnego testu ekranów.");
        }

        var rollback = _pendingTestRollbackLayout;
        _pendingTestRollbackLayout = null;
        var result = await _layoutService.ApplyLayoutAsync(rollback, cancellationToken);
        return result.Success
            ? DisplayTransitionResult.Ok("Przywrócono poprzedni układ ekranów.")
            : DisplayTransitionResult.Fail("RollbackFailed", result.Message);
    }

    public async Task<DisplayTransitionResult> RestorePreviousLayoutAsync(CancellationToken cancellationToken = default)
    {
        var state = await _runtimeStateService.LoadAsync(cancellationToken);
        if (state.PreviousDisplayLayout is null)
        {
            return DisplayTransitionResult.Fail("PreviousLayoutMissing", "Nie ma zapisanego poprzedniego układu ekranów.");
        }

        var result = await _layoutService.ApplyLayoutAsync(state.PreviousDisplayLayout, cancellationToken);
        return result.Success
            ? DisplayTransitionResult.Ok("Przywrócono poprzedni układ ekranów.")
            : DisplayTransitionResult.Fail("RestorePreviousFailed", result.Message);
    }

    private async Task<DisplayTransitionResult> RunModeTransitionAsync(
        GameShellMode targetMode,
        Func<DisplayLayoutSnapshot, Task<DisplayLayoutSnapshot>> buildTargetLayout,
        Func<DisplayLayoutSnapshot, bool> verify,
        CancellationToken cancellationToken,
        bool captureWorkReturnLayout = true)
    {
        var state = await _runtimeStateService.LoadAsync(cancellationToken);
        var current = await _queryService.CaptureCurrentLayoutAsync(cancellationToken);
        var target = await buildTargetLayout(current);

        if (DisplayLayoutVerifier.MatchesSnapshot(target, current))
        {
            state.CurrentMode = targetMode;
            if (targetMode is GameShellMode.Console or GameShellMode.PcGaming)
            {
                EnsureWorkReturnLayout(state, current);
            }

            await _runtimeStateService.SaveAsync(state, cancellationToken);
            return DisplayTransitionResult.Ok();
        }

        state.PreviousDisplayLayout = current;
        if (captureWorkReturnLayout && targetMode is GameShellMode.Console or GameShellMode.PcGaming)
        {
            EnsureWorkReturnLayout(state, current);
        }

        await _runtimeStateService.SaveAsync(state, cancellationToken);

        var validate = await _layoutService.ValidateLayoutAsync(target, cancellationToken);
        if (!validate.Success)
        {
            return await RollBackAsync(state, "ValidateFailed", validate.Message, cancellationToken);
        }

        var apply = await _layoutService.ApplyLayoutAsync(target, cancellationToken);
        if (!apply.Success)
        {
            return await RollBackAsync(state, "ApplyFailed", apply.Message, cancellationToken);
        }

        if (!await WaitForLayoutAsync(verify, targetMode.ToString(), cancellationToken))
        {
            return await RollBackAsync(state, "VerificationFailed", "Nie udało się potwierdzić docelowego układu ekranów.", cancellationToken);
        }

        state.CurrentMode = targetMode;
        await _runtimeStateService.SaveAsync(state, cancellationToken);
        return DisplayTransitionResult.Ok();
    }

    private async Task<DisplayTransitionResult> RestoreWorkLayoutAsync(
        RuntimeState state,
        IReadOnlyList<DisplayLayoutSnapshot> candidates,
        CancellationToken cancellationToken)
    {
        var current = await _queryService.CaptureCurrentLayoutAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            if (DisplayLayoutVerifier.MatchesSnapshot(candidate, current))
            {
                state.CurrentMode = GameShellMode.Work;
                await _runtimeStateService.SaveAsync(state, cancellationToken);
                return DisplayTransitionResult.Ok();
            }

            var validate = await _layoutService.ValidateLayoutAsync(candidate, cancellationToken);
            if (!validate.Success)
            {
                _logger.LogWarning("Work Mode candidate validation failed: {Message}", validate.Message);
                continue;
            }

            var apply = await _layoutService.ApplyLayoutAsync(candidate, cancellationToken);
            if (!apply.Success)
            {
                _logger.LogWarning("Work Mode candidate apply failed: {Message}", apply.Message);
                continue;
            }

            if (await WaitForLayoutAsync(actual => DisplayLayoutVerifier.MatchesSnapshot(candidate, actual), "Work", cancellationToken))
            {
                state.CurrentMode = GameShellMode.Work;
                await _runtimeStateService.SaveAsync(state, cancellationToken);
                return DisplayTransitionResult.Ok();
            }

            _logger.LogWarning("Work Mode candidate applied but verification failed.");
        }

        return DisplayTransitionResult.Fail(
            "WorkLayoutRestoreFailed",
            "Nie udalo sie przywrocic ukladu Work. Sprawdz, czy monitory sa nadal podlaczone.");
    }

    private async Task<DisplayTransitionResult> StartTestAsync(
        DisplayLayoutSnapshot current,
        DisplayLayoutSnapshot target,
        Func<DisplayLayoutSnapshot, bool> verify,
        CancellationToken cancellationToken)
    {
        if (_pendingTestRollbackLayout is not null)
        {
            await _layoutService.ApplyLayoutAsync(_pendingTestRollbackLayout, cancellationToken);
        }

        _pendingTestRollbackLayout = current;
        var validate = await _layoutService.ValidateLayoutAsync(target, cancellationToken);
        if (!validate.Success)
        {
            _pendingTestRollbackLayout = null;
            return DisplayTransitionResult.Fail("ValidateFailed", validate.Message);
        }

        var apply = await _layoutService.ApplyLayoutAsync(target, cancellationToken);
        if (!apply.Success)
        {
            var rollback = await RestorePendingTestAsync(cancellationToken);
            return DisplayTransitionResult.Fail("ApplyFailed", apply.Message, rollback.Success);
        }

        if (!await WaitForLayoutAsync(verify, "display test", cancellationToken))
        {
            var rollback = await RestorePendingTestAsync(cancellationToken);
            return DisplayTransitionResult.Fail("VerificationFailed", "Nie udało się potwierdzić docelowego układu ekranów.", rollback.Success);
        }

        return DisplayTransitionResult.Ok("Test ekranów aktywny. Zachowaj układ albo przywróć poprzedni.");
    }

    private async Task<DisplayTransitionResult> StartConsoleTestAsync(
        DisplayLayoutSnapshot current,
        IReadOnlyCollection<DisplayIdentity> consoleDisplays,
        CancellationToken cancellationToken)
    {
        if (_pendingTestRollbackLayout is not null)
        {
            await _layoutService.ApplyLayoutAsync(_pendingTestRollbackLayout, cancellationToken);
        }

        _pendingTestRollbackLayout = current;
        var apply = await _layoutService.ActivateOnlyAsync(consoleDisplays, cancellationToken);
        if (!apply.Success)
        {
            var rollback = await RestorePendingTestAsync(cancellationToken);
            return DisplayTransitionResult.Fail("ApplyFailed", apply.Message, rollback.Success);
        }

        if (!await WaitForLayoutAsync(
            actual => DisplayLayoutVerifier.IsConsoleLayout(actual, consoleDisplays),
            "Console test",
            cancellationToken))
        {
            var rollback = await RestorePendingTestAsync(cancellationToken);
            return DisplayTransitionResult.Fail("VerificationFailed", "Nie udalo sie potwierdzic docelowego ukladu ekranow.", rollback.Success);
        }

        return DisplayTransitionResult.Ok("Test ekranow aktywny. Zachowaj uklad albo przywroc poprzedni.");
    }

    private async Task<bool> WaitForLayoutAsync(
        Func<DisplayLayoutSnapshot, bool> verify,
        string context,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= VerificationAttempts; attempt++)
        {
            var actual = await _queryService.CaptureCurrentLayoutAsync(cancellationToken);
            if (verify(actual))
            {
                if (attempt > 1)
                {
                    _logger.LogInformation("Display layout verification for {Context} succeeded after {Attempt} attempts.", context, attempt);
                }

                return true;
            }

            _logger.LogDebug(
                "Display layout verification for {Context} did not match. Attempt {Attempt}/{MaxAttempts}.",
                context,
                attempt,
                VerificationAttempts);

            if (attempt < VerificationAttempts)
            {
                await Task.Delay(VerificationDelay, cancellationToken);
            }
        }

        _logger.LogWarning("Display layout verification for {Context} failed after {AttemptCount} attempts.", context, VerificationAttempts);
        return false;
    }

    private async Task<DisplayTransitionResult> RollBackAsync(RuntimeState state, string errorCode, string message, CancellationToken cancellationToken)
    {
        var rolledBack = false;
        if (state.PreviousDisplayLayout is not null)
        {
            var rollback = await _layoutService.ApplyLayoutAsync(state.PreviousDisplayLayout, cancellationToken);
            rolledBack = rollback.Success;
            _logger.LogWarning("Display transition failed with {ErrorCode}. Rollback success: {RolledBack}.", errorCode, rolledBack);
        }

        return DisplayTransitionResult.Fail(
            errorCode,
            rolledBack ? "Nie udało się przełączyć ekranów. Przywrócono poprzedni układ." : message,
            rolledBack);
    }

    private static void EnsureWorkReturnLayout(RuntimeState state, DisplayLayoutSnapshot current)
    {
        if (state.CurrentMode is GameShellMode.Work || (state.CurrentMode is null && state.WorkReturnLayout is null))
        {
            state.WorkReturnLayout = current;
        }
    }
}
