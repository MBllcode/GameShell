using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ModeOrchestratorTests
{
    [Fact]
    public async Task EnterWorkMode_RunsCleanupBeforeDisplayRestoreAndPostWorkflow()
    {
        var order = new List<string>();
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(new AppSettings()),
            new FakeModeWorkflowService(order),
            new FakeDisplayTransitionService(order),
            new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.PcGaming }),
            NullLogger<ModeOrchestrator>.Instance);

        var result = await orchestrator.EnterWorkModeAsync();

        Assert.True(result.Success);
        Assert.Equal(["prepare-work", "display-work", "post-work"], order);
    }

    [Fact]
    public async Task EnterWorkMode_DisplayFailureDoesNotRunPostWorkflowOrChangeRuntimeMode()
    {
        var order = new List<string>();
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.PcGaming });
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(new AppSettings()),
            new FakeModeWorkflowService(order),
            new FakeDisplayTransitionService(order) { WorkModeResult = DisplayTransitionResult.Fail("SetDisplayConfigFailed", "display failed") },
            runtime,
            NullLogger<ModeOrchestrator>.Instance);

        var result = await orchestrator.EnterWorkModeAsync();

        Assert.False(result.Success);
        Assert.Equal(ModeState.Error, result.CurrentState);
        Assert.Equal(["prepare-work", "display-work"], order);
        Assert.Equal(GameShellMode.PcGaming, runtime.State.CurrentMode);
    }

    [Fact]
    public async Task EnterWorkMode_PostWorkflowFailureIsBestEffortAfterDisplaySuccess()
    {
        var order = new List<string>();
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.PcGaming });
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(new AppSettings()),
            new FakeModeWorkflowService(order) { PostWorkResult = ModeWorkflowResult.Fail("desktop icons failed") },
            new FakeDisplayTransitionService(order),
            runtime,
            NullLogger<ModeOrchestrator>.Instance);

        var result = await orchestrator.EnterWorkModeAsync();

        Assert.True(result.Success);
        Assert.Equal(ModeState.WorkMode, result.CurrentState);
        Assert.Equal(GameShellMode.Work, runtime.State.CurrentMode);
    }

    [Fact]
    public async Task EnterWorkMode_CallerCancellationReturnsCancelledResult()
    {
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.PcGaming });
        var display = new FakeDisplayTransitionService { BlockUntilCancellation = true };
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(new AppSettings()),
            new FakeModeWorkflowService(),
            display,
            runtime,
            NullLogger<ModeOrchestrator>.Instance);

        using var cancellation = new CancellationTokenSource();
        var transition = orchestrator.EnterWorkModeAsync(cancellation.Token);
        cancellation.Cancel();
        var result = await transition;

        Assert.Equal(ModeResultStatus.Cancelled, result.Status);
        Assert.Equal(ModeState.Unknown, result.CurrentState);
        Assert.Equal("Cancelled", runtime.State.LastResult);
    }

    [Fact]
    public async Task EnterWorkMode_DoesNotWaitForLegacyCompletionDelay()
    {
        var settings = new AppSettings
        {
            WorkMode = { CompletionDelaySeconds = 600 }
        };
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(settings),
            new FakeModeWorkflowService(),
            new FakeDisplayTransitionService(),
            new FakeRuntimeStateService(new RuntimeState()),
            NullLogger<ModeOrchestrator>.Instance);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var result = await orchestrator.EnterWorkModeAsync(cancellation.Token);

        Assert.True(result.Success);
        Assert.Equal(ModeState.WorkMode, result.CurrentState);
        Assert.True(result.DurationMs < 200);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly AppSettings _settings;

        public FakeSettingsService(AppSettings settings)
        {
            _settings = settings;
        }

        public string SettingsPath => "fake-settings.json";

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_settings);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeModeWorkflowService : IModeWorkflowService
    {
        private readonly List<string>? _order;

        public FakeModeWorkflowService(List<string>? order = null)
        {
            _order = order;
        }

        public ModeWorkflowResult PostWorkResult { get; init; } = ModeWorkflowResult.Ok();

        public Task<ModeWorkflowResult> PrepareWorkWorkflowAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            _order?.Add("prepare-work");
            return Task.FromResult(ModeWorkflowResult.Ok());
        }

        public Task<ModeWorkflowResult> PrepareGamingWorkflowAsync(
            AppSettings settings,
            GameShellMode mode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ModeWorkflowResult.Ok());
        }

        public Task<ModeWorkflowResult> CompleteGamingWorkflowAsync(
            AppSettings settings,
            GameShellMode mode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ModeWorkflowResult.Ok());
        }

        public Task<ModeWorkflowResult> RunGamingWorkflowAsync(
            AppSettings settings,
            GameShellMode mode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ModeWorkflowResult.Ok());
        }

        public Task<ModeWorkflowResult> RunWorkWorkflowAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            _order?.Add("post-work");
            return Task.FromResult(PostWorkResult);
        }
    }

    private sealed class FakeDisplayTransitionService : IDisplayTransitionService
    {
        private readonly List<string>? _order;

        public FakeDisplayTransitionService(List<string>? order = null)
        {
            _order = order;
        }

        public DisplayTransitionResult WorkModeResult { get; init; } = DisplayTransitionResult.Ok();
        public bool BlockUntilCancellation { get; init; }

        public Task<DisplayTransitionResult> EnterConsoleModeAsync(
            DisplayIdentity? consoleDisplay,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DisplayTransitionResult.Ok());
        }

        public Task<DisplayTransitionResult> EnterPcGamingModeAsync(
            DisplayLayoutSnapshot? pcGamingLayout,
            DisplayIdentity? fallbackConsoleDisplay,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DisplayTransitionResult.Ok());
        }

        public async Task<DisplayTransitionResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
        {
            _order?.Add("display-work");
            if (BlockUntilCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return WorkModeResult;
        }

        public Task<DisplayTransitionResult> TestConsoleDisplayAsync(
            DisplayIdentity? consoleDisplay,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DisplayTransitionResult.Ok());
        }

        public Task<DisplayTransitionResult> TestPcGamingLayoutAsync(
            DisplayLayoutSnapshot? pcGamingLayout,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DisplayTransitionResult.Ok());
        }

        public Task<DisplayTransitionResult> ConfirmPendingTestAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DisplayTransitionResult.Ok());
        }

        public Task<DisplayTransitionResult> RestorePendingTestAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DisplayTransitionResult.Ok());
        }

        public Task<DisplayTransitionResult> RestorePreviousLayoutAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DisplayTransitionResult.Ok());
        }

        public Task<RuntimeState> LoadRuntimeStateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RuntimeState());
        }
    }

    private sealed class FakeRuntimeStateService : IRuntimeStateService
    {
        public FakeRuntimeStateService(RuntimeState state)
        {
            State = state;
        }

        public RuntimeState State { get; private set; }
        public string StatePath => "fake-state.json";

        public Task<RuntimeState> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(State);
        }

        public Task SaveAsync(RuntimeState state, CancellationToken cancellationToken = default)
        {
            State = state;
            return Task.CompletedTask;
        }
    }
}
