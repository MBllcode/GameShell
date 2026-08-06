using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ModeOrchestratorTests
{
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
            return Task.FromResult(ModeWorkflowResult.Ok());
        }
    }

    private sealed class FakeDisplayTransitionService : IDisplayTransitionService
    {
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

        public Task<DisplayTransitionResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DisplayTransitionResult.Ok());
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
