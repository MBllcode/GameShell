using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Services;

public sealed class ShellStartupHealthService
{
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(10);
    private const int RecoveryThreshold = 3;

    private readonly IRuntimeStateService _runtimeStateService;
    private readonly ILogger<ShellStartupHealthService> _logger;

    public ShellStartupHealthService(
        IRuntimeStateService runtimeStateService,
        ILogger<ShellStartupHealthService> logger)
    {
        _runtimeStateService = runtimeStateService;
        _logger = logger;
    }

    public async Task<ShellStartupHealthResult> BeginAttemptAsync(CancellationToken cancellationToken = default)
    {
        var state = await _runtimeStateService.LoadAsync(cancellationToken);
        var now = DateTimeOffset.Now;
        var previousIncomplete = state.ShellStartupAttemptStarted is { } started
            && (state.ShellStartupCompleted is null || state.ShellStartupCompleted < started)
            && now - started <= FailureWindow;

        state.ConsecutiveShellStartupFailures = previousIncomplete
            ? state.ConsecutiveShellStartupFailures + 1
            : 1;
        state.ShellStartupAttemptStarted = now;
        await _runtimeStateService.SaveAsync(state, cancellationToken);

        var recover = state.ConsecutiveShellStartupFailures >= RecoveryThreshold;
        _logger.LogInformation(
            "[SHELL STARTUP HEALTH] Attempt={Attempt} PreviousIncomplete={PreviousIncomplete} Recovery={Recovery}",
            state.ConsecutiveShellStartupFailures,
            previousIncomplete,
            recover);
        return new ShellStartupHealthResult(state.ConsecutiveShellStartupFailures, recover);
    }

    public async Task CompleteAttemptAsync(CancellationToken cancellationToken = default)
    {
        var state = await _runtimeStateService.LoadAsync(cancellationToken);
        state.ShellStartupCompleted = DateTimeOffset.Now;
        state.ConsecutiveShellStartupFailures = 0;
        await _runtimeStateService.SaveAsync(state, cancellationToken);
        _logger.LogInformation("[SHELL STARTUP HEALTH] Result=Completed");
    }
}

public sealed record ShellStartupHealthResult(int Attempt, bool RequiresRecovery);
