using GameShell.Windows.Services;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class LauncherFocusRecoveryCoordinatorTests
{
    [Fact]
    public async Task HealthyForeground_ReturnsWithoutRecoveryAttempt()
    {
        var inspection = HealthyInspection();
        var focusCalls = 0;
        var coordinator = CreateCoordinator();

        var result = await coordinator.RecoverAsync(() => inspection, _ => focusCalls++);

        Assert.True(result.Success);
        Assert.False(result.RequiresFallback);
        Assert.Equal(0, result.Attempts);
        Assert.Equal(0, focusCalls);
    }

    [Fact]
    public async Task MinimizedWindow_IsRestoredAndThenSucceeds()
    {
        var inspections = new Queue<LauncherFocusInspection>([
            HealthyInspection(isMinimized: true),
            HealthyInspection()
        ]);
        var focusCalls = 0;

        var result = await CreateCoordinator().RecoverAsync(() => inspections.Dequeue(), _ => focusCalls++);

        Assert.True(result.Success);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, focusCalls);
    }

    [Fact]
    public async Task BackgroundWindow_IsForegrounded()
    {
        var inspections = new Queue<LauncherFocusInspection>([
            HealthyInspection(foregroundProcessId: 999),
            HealthyInspection()
        ]);
        var focusCalls = 0;

        var result = await CreateCoordinator().RecoverAsync(() => inspections.Dequeue(), _ => focusCalls++);

        Assert.True(result.Success);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, focusCalls);
    }

    [Fact]
    public async Task FirstAttemptFails_SecondAttemptSucceeds()
    {
        var inspections = new Queue<LauncherFocusInspection>([
            HealthyInspection(foregroundProcessId: 999),
            HealthyInspection(foregroundProcessId: 999),
            HealthyInspection()
        ]);
        var focusCalls = 0;

        var result = await CreateCoordinator().RecoverAsync(() => inspections.Dequeue(), _ => focusCalls++);

        Assert.True(result.Success);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(2, focusCalls);
    }

    [Fact]
    public async Task MissingWindow_RequiresFallback()
    {
        var result = await CreateCoordinator().RecoverAsync(() => null, _ => throw new InvalidOperationException());

        Assert.False(result.Success);
        Assert.True(result.RequiresFallback);
        Assert.Equal(0, result.Attempts);
    }

    [Fact]
    public async Task UnresponsiveWindow_RequiresFallbackAfterTwoAttempts()
    {
        var result = await CreateCoordinator().RecoverAsync(
            () => new LauncherFocusInspection(42, new IntPtr(7), true, true, false, false, 999),
            _ => { });

        Assert.False(result.Success);
        Assert.True(result.RequiresFallback);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task HealthyReturn_DoesNotRequestFallbackRestart()
    {
        var result = await CreateCoordinator().RecoverAsync(() => HealthyInspection(), _ => throw new InvalidOperationException());

        Assert.True(result.Success);
        Assert.False(result.RequiresFallback);
    }

    private static LauncherFocusRecoveryCoordinator CreateCoordinator() =>
        new((_, _) => Task.CompletedTask);

    private static LauncherFocusInspection HealthyInspection(
        bool isMinimized = false,
        uint foregroundProcessId = 42) =>
        new(42, new IntPtr(7), true, true, isMinimized, true, foregroundProcessId);
}