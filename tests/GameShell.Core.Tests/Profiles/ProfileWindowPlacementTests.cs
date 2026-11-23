using GameShell.Windows.Desktop;
using Xunit;

namespace GameShell.Core.Tests;
public sealed class ProfileWindowPlacementTests
{
    [Theory]
    [InlineData("Playnite.DesktopApp.exe", "Playnite.FullscreenApp.exe")]
    [InlineData("Playnite.FullscreenApp.exe", "Playnite.DesktopApp.exe")]
    [InlineData("Audacity.exe", null)]
    public void PlayniteWindowLookupUsesBothModesFromTheSameInstallation(string configured, string? alternate)
    {
        const string installation = @"D:\Games\Playnite";
        var executable = Path.Combine(installation, configured);
        Assert.Equal(alternate is null ? null : Path.Combine(installation, alternate),
            PlayniteSessionApplication.GetAlternateExecutable(executable));
    }

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
        var ticks = 0;
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => WindowsWindowManagementService.StabilizeApplicationWindowAsync(
            () => IntPtr.Zero, _ => false, _ => { }, CancellationToken.None, () => { ticks++; return Task.CompletedTask; }));
        Assert.Contains("No application window", exception.Message);
        Assert.Equal(50, ticks);
    }

    [Fact]
    public async Task ReusedWindowNeedsOnlyTwoStableSamplesAfterPlacement()
    {
        var ticks = 0;
        var placements = 0;
        await WindowsWindowManagementService.StabilizeApplicationWindowAsync(
            () => new IntPtr(1), _ => true, _ => placements++, CancellationToken.None,
            () => { ticks++; return Task.CompletedTask; }, observeStartup: false);
        Assert.Equal(1, placements);
        Assert.Equal(2, ticks);
    }
}
