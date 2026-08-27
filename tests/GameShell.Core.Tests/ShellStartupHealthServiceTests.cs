using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ShellStartupHealthServiceTests
{
    [Fact]
    public async Task ThirdConsecutiveIncompleteAttempt_RequiresRecovery()
    {
        var statePath = CreateStatePath();
        var runtime = new RuntimeStateService(NullLogger<RuntimeStateService>.Instance, statePath);
        var service = new ShellStartupHealthService(runtime, NullLogger<ShellStartupHealthService>.Instance);

        Assert.False((await service.BeginAttemptAsync()).RequiresRecovery);
        Assert.False((await service.BeginAttemptAsync()).RequiresRecovery);
        Assert.True((await service.BeginAttemptAsync()).RequiresRecovery);
    }

    [Fact]
    public async Task CompletedAttempt_ResetsCrashLoopCounter()
    {
        var statePath = CreateStatePath();
        var runtime = new RuntimeStateService(NullLogger<RuntimeStateService>.Instance, statePath);
        var service = new ShellStartupHealthService(runtime, NullLogger<ShellStartupHealthService>.Instance);

        await service.BeginAttemptAsync();
        await service.BeginAttemptAsync();
        await service.CompleteAttemptAsync();
        var next = await service.BeginAttemptAsync();

        Assert.Equal(1, next.Attempt);
        Assert.False(next.RequiresRecovery);
    }

    [Fact]
    public async Task StaleIncompleteAttempt_DoesNotTriggerRecoveryLoop()
    {
        var statePath = CreateStatePath();
        var runtime = new RuntimeStateService(NullLogger<RuntimeStateService>.Instance, statePath);
        await runtime.SaveAsync(new RuntimeState
        {
            ShellStartupAttemptStarted = DateTimeOffset.Now.AddMinutes(-20),
            ConsecutiveShellStartupFailures = 2
        });
        var service = new ShellStartupHealthService(runtime, NullLogger<ShellStartupHealthService>.Instance);

        var result = await service.BeginAttemptAsync();

        Assert.Equal(1, result.Attempt);
        Assert.False(result.RequiresRecovery);
    }

    private static string CreateStatePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GameShell.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "state.json");
    }
}
