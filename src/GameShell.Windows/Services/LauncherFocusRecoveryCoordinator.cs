namespace GameShell.Windows.Services;

public sealed record LauncherFocusInspection(
    int ProcessId,
    IntPtr WindowHandle,
    bool WindowExists,
    bool IsVisible,
    bool IsMinimized,
    bool IsResponding,
    uint ForegroundProcessId)
{
    public bool IsHealthy => WindowExists && IsVisible && !IsMinimized && IsResponding;
    public bool IsForeground => WindowExists && ForegroundProcessId == (uint)ProcessId;
}

public sealed record LauncherFocusRecoveryResult(
    bool Success,
    bool RequiresFallback,
    int Attempts,
    LauncherFocusInspection? FinalInspection);

public sealed class LauncherFocusRecoveryCoordinator
{
    private const int MaximumAttempts = 2;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public LauncherFocusRecoveryCoordinator(
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _delay = delay ?? ((duration, cancellationToken) => Task.Delay(duration, cancellationToken));
    }

    public async Task<LauncherFocusRecoveryResult> RecoverAsync(
        Func<LauncherFocusInspection?> inspect,
        Action<IntPtr> restoreAndFocus,
        Action<int, LauncherFocusInspection?>? logAttempt = null,
        CancellationToken cancellationToken = default)
    {
        var inspection = inspect();
        if (inspection is { IsHealthy: true, IsForeground: true })
        {
            logAttempt?.Invoke(0, inspection);
            return new(true, false, 0, inspection);
        }

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            if (inspection is null || !inspection.WindowExists)
            {
                break;
            }

            if (attempt > 1)
            {
                await _delay(TimeSpan.FromMilliseconds(650), cancellationToken);
            }

            restoreAndFocus(inspection.WindowHandle);
            await _delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            inspection = inspect();
            logAttempt?.Invoke(attempt, inspection);

            if (inspection is { IsHealthy: true, IsForeground: true })
            {
                return new(true, false, attempt, inspection);
            }
        }

        return new(false, true, Math.Min(MaximumAttempts, inspection is null ? 0 : MaximumAttempts), inspection);
    }
}
