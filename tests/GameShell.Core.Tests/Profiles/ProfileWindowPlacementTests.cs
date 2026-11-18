using GameShell.Windows.Desktop;
using Xunit;

namespace GameShell.Core.Tests;
public sealed class ProfileWindowPlacementTests
{
    [Fact]
    public async Task StartupReplacesEarlyWindowAndOverridesRestoredMonitor()
    {
        var tick = 0;
        var placed = new List<(int Tick, IntPtr Window)>();
        var positionCorrect = false;
        await WindowsWindowManagementService.StabilizeApplicationWindowAsync(
            () => tick < 2 ? IntPtr.Zero : tick < 7 ? new IntPtr(1) : new IntPtr(2),
            _ => positionCorrect,
            window => { placed.Add((tick, window)); positionCorrect = true; },
            CancellationToken.None,
            () => { tick++; if (tick == 12) positionCorrect = false; return Task.CompletedTask; });
        Assert.Contains(placed, item => item.Window == new IntPtr(2));
        Assert.Contains(placed, item => item.Tick == 12);
        Assert.True(tick >= 18);
    }

    [Fact]
    public async Task MissingFinalWindowReportsFailure()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => WindowsWindowManagementService.StabilizeApplicationWindowAsync(
            () => IntPtr.Zero, _ => false, _ => { }, CancellationToken.None, () => Task.CompletedTask));
    }
}
