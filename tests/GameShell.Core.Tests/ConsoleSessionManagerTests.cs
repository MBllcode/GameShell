using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ConsoleSessionManagerTests
{
    [Fact]
    public async Task EnterProfile_ActivatesLauncherAndTracksGameLifecycle()
    {
        var orchestrator = new FakeModeOrchestrator();
        var monitor = new FakeGameSessionMonitor();
        using var manager = CreateManager(orchestrator, monitor);

        var result = await manager.EnterProfileAsync("LivingRoom");
        Assert.True(result.Success);
        Assert.Equal(ConsoleSessionState.LauncherActive, manager.GetSnapshot().State);

        monitor.RaiseGameDetected();
        Assert.Equal(ConsoleSessionState.GameActive, manager.GetSnapshot().State);

        monitor.RaiseGameExited();
        await WaitUntilAsync(() => manager.GetSnapshot().State == ConsoleSessionState.LauncherActive);
        Assert.Equal(1, orchestrator.ProfileActivations);
    }

    [Fact]
    public async Task DuplicateActivation_IsRejectedWithoutSecondOrchestratorCall()
    {
        var orchestrator = new FakeModeOrchestrator();
        using var manager = CreateManager(orchestrator, new FakeGameSessionMonitor());

        await manager.EnterProfileAsync("LivingRoom");
        var result = await manager.EnterProfileAsync("Office");

        Assert.Equal(ModeResultStatus.AlreadyRunning, result.Status);
        Assert.Equal(1, orchestrator.ProfileActivations);
    }

    [Fact]
    public async Task WorkMode_StopsMonitorAndReturnsToIdle()
    {
        var monitor = new FakeGameSessionMonitor();
        var orchestrator = new FakeModeOrchestrator();
        using var manager = CreateManager(orchestrator, monitor);

        await manager.EnterProfileAsync("LivingRoom");
        var result = await manager.EnterWorkModeAsync();

        Assert.True(result.Success);
        Assert.Equal(ConsoleSessionState.Idle, manager.GetSnapshot().State);
        Assert.Equal(1, monitor.StopCalls);
        Assert.Equal(1, orchestrator.WorkActivations);
    }

    [Fact]
    public async Task GameDetected_OnlyChangesSessionState()
    {
        var monitor = new FakeGameSessionMonitor();
        using var manager = CreateManager(new FakeModeOrchestrator(), monitor);

        await manager.EnterProfileAsync("LivingRoom");
        monitor.RaiseGameDetected(100);

        Assert.Equal(ConsoleSessionState.GameActive, manager.GetSnapshot().State);
    }

    [Fact]
    public async Task FailedLauncherRecovery_LeavesSessionFaulted()
    {
        var monitor = new FakeGameSessionMonitor();
        using var manager = CreateManager(
            new FakeModeOrchestrator(),
            monitor,
            new FakeLauncherRecoveryStrategy { ShouldFail = true });

        await manager.EnterProfileAsync("LivingRoom");
        monitor.RaiseGameDetected();
        monitor.RaiseGameExited();
        await WaitUntilAsync(() => manager.GetSnapshot().State == ConsoleSessionState.Faulted);

        Assert.Equal(ConsoleSessionState.Faulted, manager.GetSnapshot().State);
    }

    private static ConsoleSessionManager CreateManager(
        FakeModeOrchestrator orchestrator,
        FakeGameSessionMonitor monitor,
        FakeLauncherRecoveryStrategy? recovery = null)
    {
        return new ConsoleSessionManager(
            orchestrator,
            new FakeSettingsService(),
            monitor,
            recovery ?? new FakeLauncherRecoveryStrategy(),
            NullLogger<ConsoleSessionManager>.Instance);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 20 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }
    }

    private sealed class FakeGameSessionMonitor : IGameSessionMonitor
    {
        public event EventHandler<GameSessionEventArgs>? GameDetected;
        public event EventHandler<GameSessionEventArgs>? GameExited;
        public int StopCalls { get; private set; }

        public Task StartAsync(IReadOnlyCollection<string> processNames, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalls++;
            return Task.CompletedTask;
        }

        public void RaiseGameDetected(int processId = 100) => GameDetected?.Invoke(this, new GameSessionEventArgs(processId, "game"));
        public void RaiseGameExited(int processId = 100) => GameExited?.Invoke(this, new GameSessionEventArgs(processId, "game"));
    }

    private sealed class FakeLauncherRecoveryStrategy : ILauncherRecoveryStrategy
    {
        public bool ShouldFail { get; init; }

        public Task<LauncherRecoveryResult> RecoverAsync(
            AppSettings settings,
            string? profileName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ShouldFail
                ? LauncherRecoveryResult.Failed("recovery failed")
                : LauncherRecoveryResult.Recovered("Launcher recovered normally"));
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public string SettingsPath => "test";
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppSettings
            {
                PcGaming = { GameProcessNames = ["game.exe"] },
                Display =
                {
                    PcGamingProfiles =
                    [
                        new DisplayLayoutProfile
                        {
                            Name = "LivingRoom",
                            Layout = new DisplayLayoutSnapshot
                            {
                                Displays =
                                [
                                    new DisplayPathSnapshot
                                    {
                                        IsActive = true,
                                        IsPrimary = true,
                                        Width = 1920,
                                        Height = 1080
                                    }
                                ]
                            }
                        }
                    ]
                }
            });
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeModeOrchestrator : IModeOrchestrator
    {
        public int ProfileActivations { get; private set; }
        public int ConsoleActivations { get; private set; }
        public int WorkActivations { get; private set; }

        public Task<AppStatus> GetStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AppStatus());

        public Task<ModeResult> EnterGameShellAsync(CancellationToken cancellationToken = default)
        {
            ConsoleActivations++;
            return Task.FromResult(SuccessResult());
        }

        public Task<ModeResult> EnterGameShellWithDisplayProfileAsync(string profileName, CancellationToken cancellationToken = default)
        {
            ProfileActivations++;
            return Task.FromResult(SuccessResult());
        }

        public Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
        {
            WorkActivations++;
            return Task.FromResult(SuccessResult(ModeState.WorkMode));
        }

        private static ModeResult SuccessResult(ModeState state = ModeState.GameShell) => new()
        {
            Status = ModeResultStatus.Success,
            CurrentState = state
        };
    }
}
