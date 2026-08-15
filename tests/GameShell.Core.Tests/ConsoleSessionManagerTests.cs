using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using GameShell.Windows.Services;
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
    public async Task SameProfileActivationWithoutSavedChanges_IsReportedAsNoChanges()
    {
        var orchestrator = new FakeModeOrchestrator();
        using var manager = CreateManager(orchestrator, new FakeGameSessionMonitor());

        await manager.EnterProfileAsync("LivingRoom");
        var result = await manager.EnterProfileAsync("LivingRoom");

        Assert.Equal(ModeResultStatus.NoChanges, result.Status);
        Assert.Equal(1, orchestrator.ProfileActivations);
    }

    [Fact]
    public async Task SameProfileActivationAfterSavedChanges_ReloadsTheProfile()
    {
        var settings = new FakeSettingsService();
        var orchestrator = new FakeModeOrchestrator();
        var monitor = new FakeGameSessionMonitor();
        using var manager = CreateManager(orchestrator, monitor, settings: settings);

        await manager.EnterProfileAsync("LivingRoom");
        settings.ProfileWidth = 2560;
        var result = await manager.EnterProfileAsync("LivingRoom");

        Assert.True(result.Success);
        Assert.Equal(2, orchestrator.ProfileActivations);
        Assert.Equal(1, monitor.StopCalls);
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
    public async Task SuccessfulWorkMode_CommitsIdleWithoutActiveProfile()
    {
        var monitor = new FakeGameSessionMonitor();
        var orchestrator = new FakeModeOrchestrator();
        using var manager = CreateManager(orchestrator, monitor);

        await manager.EnterProfileAsync("PC");
        monitor.RaiseGameDetected();
        var result = await manager.EnterWorkModeAsync();

        Assert.True(result.Success);
        var snapshot = manager.GetSnapshot();
        Assert.Equal(ConsoleSessionState.Idle, snapshot.State);
        Assert.Null(snapshot.ProfileName);
        Assert.True(snapshot.SessionGeneration > 0);
    }

    [Fact]
    public async Task DelayedCallbacksFromClosedGamingSession_DoNotRestoreLauncherState()
    {
        var monitor = new FakeGameSessionMonitor();
        using var manager = CreateManager(new FakeModeOrchestrator(), monitor);

        await manager.EnterProfileAsync("PC");
        await manager.EnterWorkModeAsync();
        monitor.RaiseGameDetected();
        monitor.RaiseGameExited();
        await Task.Delay(30);

        Assert.Equal(ConsoleSessionState.Idle, manager.GetSnapshot().State);
        Assert.Null(manager.GetSnapshot().ProfileName);
    }

    [Fact]
    public async Task WorkModeFailure_LeavesSessionFaulted()
    {
        var monitor = new FakeGameSessionMonitor();
        var orchestrator = new FakeModeOrchestrator
        {
            WorkModeResult = new ModeResult
            {
                Status = ModeResultStatus.Failed,
                CurrentState = ModeState.Error,
                Message = "display restore failed"
            }
        };
        using var manager = CreateManager(orchestrator, monitor);

        await manager.EnterProfileAsync("LivingRoom");
        var result = await manager.EnterWorkModeAsync();

        Assert.False(result.Success);
        Assert.Equal(ConsoleSessionState.Faulted, manager.GetSnapshot().State);
        Assert.Equal("display restore failed", manager.GetSnapshot().LastError);
    }

    [Fact]
    public async Task WorkModeTransition_RejectsConcurrentRequest()
    {
        var monitor = new FakeGameSessionMonitor();
        var orchestrator = new FakeModeOrchestrator { BlockWorkMode = true };
        using var manager = CreateManager(orchestrator, monitor);

        await manager.EnterProfileAsync("LivingRoom");
        var first = manager.EnterWorkModeAsync();
        await orchestrator.WorkModeStarted.Task;

        var second = await manager.EnterWorkModeAsync();
        orchestrator.ReleaseWorkMode.TrySetResult();
        var firstResult = await first;

        Assert.True(firstResult.Success);
        Assert.Equal(ModeResultStatus.AlreadyRunning, second.Status);
        Assert.Equal(1, orchestrator.WorkActivations);
    }

    [Fact]
    public async Task GameSessionMonitor_StopConsumesItsOwnCancellation()
    {
        using var monitor = new WindowsGameSessionMonitor(NullLogger<WindowsGameSessionMonitor>.Instance);

        await monitor.StartAsync([]);
        await monitor.StopAsync();
    }

    [Fact]
    public async Task GameSessionMonitor_DoesNotUseActivationTokenAsMonitorLifetime()
    {
        using var activationCancellation = new CancellationTokenSource();
        using var monitor = new WindowsGameSessionMonitor(NullLogger<WindowsGameSessionMonitor>.Instance);

        await monitor.StartAsync([], activationCancellation.Token);
        activationCancellation.Cancel();
        await monitor.StopAsync();
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
        var orchestrator = new FakeModeOrchestrator();
        using var manager = CreateManager(
            orchestrator,
            monitor,
            new FakeLauncherRecoveryStrategy { ShouldFail = true });

        await manager.EnterProfileAsync("LivingRoom");
        monitor.RaiseGameDetected();
        monitor.RaiseGameExited();
        await WaitUntilAsync(() => manager.GetSnapshot().State == ConsoleSessionState.Faulted);

        Assert.Equal(ConsoleSessionState.Faulted, manager.GetSnapshot().State);
        Assert.Equal(0, orchestrator.WorkActivations);
    }

    private static ConsoleSessionManager CreateManager(
        FakeModeOrchestrator orchestrator,
        FakeGameSessionMonitor monitor,
        FakeLauncherRecoveryStrategy? recovery = null,
        FakeSettingsService? settings = null)
    {
        return new ConsoleSessionManager(
            orchestrator,
            settings ?? new FakeSettingsService(),
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
            int? gameProcessId = null,
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
        public int ProfileWidth { get; set; } = 1920;
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppSettings
            {
                Gaming = { GameProcessNames = ["game.exe"] },
                Display =
                {
                    GamingProfiles =
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
                                    Width = ProfileWidth,
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
        public TaskCompletionSource WorkModeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseWorkMode { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProfileActivations { get; private set; }
        public int ConsoleActivations { get; private set; }
        public int WorkActivations { get; private set; }
        public ModeResult WorkModeResult { get; init; } = SuccessResult(ModeState.WorkMode);
        public bool BlockWorkMode { get; init; }

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
            return EnterWorkModeCoreAsync(cancellationToken);
        }

        private async Task<ModeResult> EnterWorkModeCoreAsync(CancellationToken cancellationToken)
        {
            WorkModeStarted.TrySetResult();
            if (BlockWorkMode)
            {
                await ReleaseWorkMode.Task.WaitAsync(cancellationToken);
            }
            return WorkModeResult;
        }

        private static ModeResult SuccessResult(ModeState state = ModeState.GameShell) => new()
        {
            Status = ModeResultStatus.Success,
            CurrentState = state
        };

        private static ModeResult FailedResult(string message) => new()
        {
            Status = ModeResultStatus.Failed,
            CurrentState = ModeState.Error,
            Message = message
        };
    }
}
