using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ModeOrchestratorTests
{
    [Fact]
    public async Task CompletedFirstRun_InitializesWorkModeAndPersistsRuntimeState()
    {
        var settings = new AppSettings { Ui = { IsFirstRunCompleted = true } };
        var runtime = new FakeRuntimeStateService(new RuntimeState());
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(settings),
            new FakeModeWorkflowService(),
            new FakeDisplayTransitionService(),
            runtime,
            NullLogger<ModeOrchestrator>.Instance);

        var status = await orchestrator.GetStatusAsync();

        Assert.Equal(GameShellMode.Work.ToString(), status.CurrentMode);
        Assert.Equal(ModeState.WorkMode, status.State);
        Assert.Equal(GameShellMode.Work, runtime.State.CurrentMode);
        Assert.Equal("Initialized", runtime.State.LastResult);
    }

    [Fact]
    public async Task CompletedFirstRunOnStartup_DoesNotOverwriteActiveGamingMode()
    {
        var settings = new AppSettings { Ui = { IsFirstRunCompleted = true } };
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.GamingProfile });
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(settings),
            new FakeModeWorkflowService(),
            new FakeDisplayTransitionService(),
            runtime,
            NullLogger<ModeOrchestrator>.Instance);

        var status = await orchestrator.GetStatusAsync();

        Assert.Equal(GameShellMode.GamingProfile.ToString(), status.CurrentMode);
        Assert.Equal(ModeState.GameShell, status.State);
        Assert.Equal(GameShellMode.GamingProfile, runtime.State.CurrentMode);
    }

    [Fact]
    public async Task PersistedWorkModeOnFreshStartup_ReportsWorkState()
    {
        var settings = new AppSettings { Ui = { IsFirstRunCompleted = true } };
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(settings),
            new FakeModeWorkflowService(),
            new FakeDisplayTransitionService(),
            new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.Work }),
            NullLogger<ModeOrchestrator>.Instance);

        var status = await orchestrator.GetStatusAsync();

        Assert.Equal(GameShellMode.Work.ToString(), status.CurrentMode);
        Assert.Equal(ModeState.WorkMode, status.State);
    }

    [Fact]
    public async Task CompletedFirstRun_DoesNotHidePersistedRuntimeFault()
    {
        var settings = new AppSettings { Ui = { IsFirstRunCompleted = true } };
        var runtime = new FakeRuntimeStateService(new RuntimeState { LastResult = "Failed" });
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(settings),
            new FakeModeWorkflowService(),
            new FakeDisplayTransitionService(),
            runtime,
            NullLogger<ModeOrchestrator>.Instance);

        var status = await orchestrator.GetStatusAsync();

        Assert.Null(status.CurrentMode);
        Assert.Equal(ModeState.Unknown, status.State);
        Assert.Equal("Failed", runtime.State.LastResult);
    }

    [Fact]
    public async Task EnterWorkMode_RunsCleanupBeforeDisplayRestoreAndPostWorkflow()
    {
        var order = new List<string>();
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(new AppSettings()),
            new FakeModeWorkflowService(order),
            new FakeDisplayTransitionService(order),
            new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.GamingProfile }),
            NullLogger<ModeOrchestrator>.Instance);

        var result = await orchestrator.EnterWorkModeAsync();

        Assert.True(result.Success, result.Message);
        Assert.Equal(["prepare-work", "display-work", "post-work"], order);
    }

    [Fact]
    public async Task EnterWorkMode_PassesConfiguredDefaultDisplayLayoutToTransition()
    {
        var layout = new DisplayLayoutSnapshot
        {
            Id = "work-default",
            Displays = []
        };
        var settings = new AppSettings
        {
            WorkMode = { DefaultDisplayLayout = layout }
        };
        var display = new FakeDisplayTransitionService();
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(settings),
            new FakeModeWorkflowService(),
            display,
            new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.GamingProfile }),
            NullLogger<ModeOrchestrator>.Instance);

        var result = await orchestrator.EnterWorkModeAsync();

        Assert.True(result.Success, result.Message);
        Assert.Same(layout, display.WorkModeLayout);
    }

    [Fact]
    public async Task WorkModeStatus_ClearsActiveLauncher()
    {
        var settings = new AppSettings
        {
            Launcher = { Type = "Playnite" },
            Display =
            {
                GamingProfiles = [new DisplayLayoutProfile { Name = "PC" }],
                ActiveGamingProfileName = "PC"
            }
        };
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(settings),
            new FakeModeWorkflowService(),
            new FakeDisplayTransitionService(),
            new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.GamingProfile }),
            NullLogger<ModeOrchestrator>.Instance);

        await orchestrator.EnterWorkModeAsync();
        var status = await orchestrator.GetStatusAsync();

        Assert.Equal("Work", status.CurrentMode);
        Assert.Null(status.ActiveLauncher);
    }

    [Fact]
    public async Task EnterWorkMode_DisplayFailureDoesNotRunPostWorkflowOrChangeRuntimeMode()
    {
        var order = new List<string>();
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.GamingProfile });
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
        Assert.Equal(GameShellMode.GamingProfile, runtime.State.CurrentMode);
    }

    [Fact]
    public async Task GamingDisplayFailureFromWork_RestoresTheFullWorkWorkflow()
    {
        var order = new List<string>();
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.Work });
        var display = new FakeDisplayTransitionService(order)
        {
            ConsoleResult = DisplayTransitionResult.Fail("ApplyFailed", "display failed")
        };
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(new AppSettings()),
            new FakeModeWorkflowService(order),
            display,
            runtime,
            NullLogger<ModeOrchestrator>.Instance);

        var result = await orchestrator.EnterGameShellAsync();

        Assert.False(result.Success);
        Assert.Equal(["post-work"], order);
        Assert.Equal(GameShellMode.Work, runtime.State.CurrentMode);
    }

    [Fact]
    public async Task EnterWorkMode_PostWorkflowFailureIsBestEffortAfterDisplaySuccess()
    {
        var order = new List<string>();
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.GamingProfile });
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
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.GamingProfile });
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

    [Fact]
    public async Task EnterProfile_BlockedReadinessDoesNotCallDisplayTransition()
    {
        var settings = new AppSettings
        {
            Display =
            {
                GamingProfiles =
                [
                    new DisplayLayoutProfile
                    {
                        Name = "KONSOLA",
                        DisplayTopology = new GamingProfileDisplayTopology
                        {
                            Displays =
                            [
                                new ProfileDisplayTarget
                                {
                                    Identity = new DisplayIdentity { AdapterLuid = 1, TargetId = 7, EdidManufacturerId = 123, EdidProductCodeId = 456 },
                                    Enabled = true,
                                    Width = 3840,
                                    Height = 2160,
                                    RefreshRateNumerator = 144,
                                    RefreshRateDenominator = 1
                                }
                            ]
                        }
                    }
                ]
            }
        };
        var display = new FakeDisplayTransitionService();
        var query = new FakeReadinessDisplayQuery();
        var evaluator = new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance);
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(settings),
            new FakeModeWorkflowService(),
            display,
            new FakeRuntimeStateService(new RuntimeState()),
            evaluator,
            NullLogger<ModeOrchestrator>.Instance);

        var result = await orchestrator.EnterGameShellWithDisplayProfileAsync("KONSOLA");

        Assert.Equal(ModeResultStatus.InvalidConfiguration, result.Status);
        Assert.Contains("zapisany tryb profilu", result.Message);
        Assert.Equal(0, display.GamingCount);
    }

    [Fact]
    public async Task EnterProfile_AutoHighestBelowBestKnownStillAppliesProfile()
    {
        var settings = new AppSettings
        {
            Display =
            {
                GamingProfiles =
                [
                    new DisplayLayoutProfile
                    {
                        Name = "KONSOLA",
                        DisplayTopology = new GamingProfileDisplayTopology
                        {
                            Displays =
                            [
                                new ProfileDisplayTarget
                                {
                                    Identity = new DisplayIdentity { FriendlyName = "Model X", AdapterLuid = 1, TargetId = 7, EdidIdsValid = true, EdidManufacturerId = 123, EdidProductCodeId = 456 },
                                    Enabled = true,
                                    Width = 3840,
                                    Height = 2160,
                                    RefreshPolicy = RefreshPolicy.AutoHighest,
                                    CapabilityExpectation = new DisplayCapabilityExpectation
                                    {
                                        Width = 3840,
                                        Height = 2160,
                                        LastKnownGoodMaxRefreshNumerator = 144,
                                        LastKnownGoodMaxRefreshDenominator = 1
                                    }
                                }
                            ]
                        }
                    }
                ]
            }
        };
        var display = new FakeDisplayTransitionService();
        var query = new FakeReadinessDisplayQuery();
        var evaluator = new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance);
        var orchestrator = new ModeOrchestrator(
            new FakeSettingsService(settings),
            new FakeModeWorkflowService(),
            display,
            new FakeRuntimeStateService(new RuntimeState()),
            evaluator,
            NullLogger<ModeOrchestrator>.Instance);

        var result = await orchestrator.EnterGameShellWithDisplayProfileAsync("KONSOLA");

        Assert.Equal(ModeResultStatus.Success, result.Status);
        Assert.True(result.Success);
        Assert.Equal(1, display.GamingCount);
        Assert.Equal(144, settings.Display.KnownPhysicalDisplays.Single().BestKnownCapability!.MaxRefreshRateHz);
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
        public DisplayTransitionResult ConsoleResult { get; init; } = DisplayTransitionResult.Ok();
        public bool BlockUntilCancellation { get; init; }
        public int GamingCount { get; private set; }
        public DisplayLayoutSnapshot? WorkModeLayout { get; private set; }
        public Action? OnGaming { get; set; }

        public Task<DisplayTransitionResult> EnterConsoleModeAsync(
            DisplayIdentity? consoleDisplay,
            CancellationToken cancellationToken = default)
        {
            GamingCount++;
            OnGaming?.Invoke();
            return Task.FromResult(ConsoleResult);
        }

        public Task<DisplayTransitionResult> EnterGamingModeAsync(
            DisplayLayoutSnapshot? gamingLayout,
            DisplayIdentity? fallbackConsoleDisplay,
            CancellationToken cancellationToken = default)
        {
            GamingCount++;
            OnGaming?.Invoke();
            return Task.FromResult(DisplayTransitionResult.Ok());
        }

        public async Task<DisplayTransitionResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
        {
            return await EnterWorkModeAsync(null, cancellationToken);
        }

        public async Task<DisplayTransitionResult> EnterWorkModeAsync(
            DisplayLayoutSnapshot? defaultDisplayLayout,
            CancellationToken cancellationToken = default)
        {
            WorkModeLayout = defaultDisplayLayout;
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

        public Task<DisplayTransitionResult> TestGamingLayoutAsync(
            DisplayLayoutSnapshot? gamingLayout,
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

    private sealed class FakeReadinessDisplayQuery : IDisplayQueryService
    {
        public Task<IReadOnlyList<DisplayDeviceInfo>> GetAvailableDisplaysAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DisplayDeviceInfo>>
            ([new DisplayDeviceInfo
            {
                Name = "S90F",
                Identity = new DisplayIdentity { FriendlyName = "S90F", AdapterLuid = 1, TargetId = 7, EdidManufacturerId = 123, EdidProductCodeId = 456 },
                SourceDeviceName = @"\\.\DISPLAY1",
                AdapterLuid = 1,
                TargetId = 7,
                IsEnabled = true,
                IsAvailable = true
            }]);
        }

        public Task<DisplayLayoutSnapshot> CaptureCurrentLayoutAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DisplayLayoutSnapshot());
        }

        public Task<DisplayModeQueryResult> GetAvailableDisplayModesAsync(DisplayIdentity display, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DisplayModeQueryResult
            {
                Success = true,
                Modes = [new DisplayModeInfo { Width = 3840, Height = 2160, RefreshRateNumerator = 60, RefreshRateDenominator = 1 }]
            });
        }
    }

}
