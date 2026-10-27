using GameShell.Core.Displays;
using GameShell.Core.Profiles;
using GameShell.Core.Runtime;
using GameShell.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class StartupReconciliationTests
{
    [Fact]
    public async Task ExplicitWorkStartup_RestoresEnvironmentEvenWhenDisplaysAlreadyMatch()
    {
        var work = Snapshot(Path("PC", "PC", 0, 0, 1920, 1080, true));
        var session = new FakeSessionManager();
        var service = CreateService(work, new RuntimeState { CurrentMode = GameShellMode.GamingProfile }, session);
        var result = await service.ActivateTargetAsync(Settings(work), StartupTarget.Work());
        Assert.True(result?.Success);
        Assert.Equal(1, session.WorkCalls);
    }

    [Fact]
    public async Task ExplicitWorkStartup_WithoutSavedTopologyStillRestoresEnvironment()
    {
        var session = new FakeSessionManager();
        var service = CreateService(new DisplayLayoutSnapshot(), new RuntimeState(), session);
        var result = await service.ActivateTargetAsync(new AppSettings(), StartupTarget.Work());
        Assert.True(result?.Success);
        Assert.Equal(1, session.WorkCalls);
    }

    [Fact]
    public async Task ConfiguredWorkLayout_TakesPriorityOverMatchingOldReturnLayout()
    {
        var work = Snapshot(Path("PC", "PC", 0, 0, 1920, 1080, true));
        var old = Snapshot(Path("TV", "TV", 0, 0, 3840, 2160, true));
        var query = new SequenceDisplayQueryService(old, work, work);
        var session = new FakeSessionManager();
        var service = CreateService(query, new RuntimeState { WorkReturnLayout = old }, session);
        var result = await service.ReconcileAsync(Settings(work), false);
        Assert.Null(result);
        Assert.Equal(1, session.WorkCalls);
        Assert.Equal(3, query.CaptureCount);
    }

    [Fact]
    public async Task MissingProfileAtActivation_FallsBackToActualWorkPipeline()
    {
        var work = Snapshot(Path("PC", "PC", 0, 0, 1920, 1080, true));
        var session = new FakeSessionManager();
        var service = CreateService(work, new RuntimeState(), session);
        var result = await service.ActivateTargetAsync(Settings(work), StartupTarget.Profile("deleted"));
        Assert.True(result?.Success);
        Assert.Equal(1, session.WorkCalls);
        Assert.Empty(session.ProfileCalls);
    }

    [Fact]
    public async Task ProfileStartup_InitiatesDiscoveryBeforeActivation()
    {
        var session = new FakeSessionManager();
        var discovery = new InitialDiscovery();
        var work = Snapshot(Path("PC", "PC", 0, 0, 1920, 1080, true));
        var service = new StartupReconciliationService(new FakeRuntimeStateService(new RuntimeState()),
            session, new FakeDisplayQueryService(work), discovery, NullLogger<StartupReconciliationService>.Instance);
        var settings = Settings(work);
        settings.Display.GamingProfiles = [new DisplayLayoutProfile { Id = "tv", Name = "TV" }];
        var result = await service.ActivateTargetAsync(settings, StartupTarget.Profile("tv"));
        Assert.True(result?.Success);
        Assert.True(discovery.Initialized);
        Assert.Equal(["TV"], session.ProfileCalls);
    }

    private sealed class InitialDiscovery : IDisplayDiscoveryStabilizationService
    {
        public bool Initialized { get; private set; }
        public DisplayDiscoveryState State => Initialized ? DisplayDiscoveryState.Ready : DisplayDiscoveryState.Loading;
        public Task WaitForSettledAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Discovery was not initiated");
        public Task EnsureStableAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Assert.True(timeout >= TimeSpan.FromSeconds(15));
            Initialized = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task IdleStartupWithWorkTopologyDoesNotRestoreAgain()
    {
        var work = Snapshot(
            Path("DELL", "PATH-DELL", 0, 0, 1920, 1080),
            Path("PL", "PATH-PL", 1920, 0, 3440, 1440, isPrimary: true));
        var session = new FakeSessionManager();
        var service = CreateService(work, new RuntimeState { CurrentMode = GameShellMode.Work }, session);

        var result = await service.ReconcileAsync(
            Settings(work),
            explicitGamingStartup: false);

        Assert.Null(result);
        Assert.Equal(0, session.WorkCalls);
    }

    [Fact]
    public async Task IdleStartupWithStaleGamingTopologyUsesExistingWorkModePathOnce()
    {
        var work = Snapshot(
            Path("DELL", "PATH-DELL", 0, 0, 1920, 1080),
            Path("PL", "PATH-PL", 1920, 0, 3440, 1440, isPrimary: true));
        var stale = Snapshot(Path("S90F", "PATH-TV", 0, 0, 3840, 2160, isPrimary: true));
        var query = new SequenceDisplayQueryService(stale, work, work);
        var session = new FakeSessionManager();
        var service = CreateService(query, new RuntimeState { CurrentMode = GameShellMode.Work }, session);

        var result = await service.ReconcileAsync(
            Settings(work),
            explicitGamingStartup: false);

        Assert.Null(result);
        Assert.Equal(1, session.WorkCalls);
        Assert.Equal(3, query.CaptureCount);
    }

    [Fact]
    public async Task RecoverableGamingRuntimeIsPreservedAtStartup()
    {
        var work = Snapshot(Path("PL", "PATH-PL", 0, 0, 3440, 1440, isPrimary: true));
        var session = new FakeSessionManager();
        var service = CreateService(
            work,
            new RuntimeState
            {
                CurrentMode = GameShellMode.GamingProfile,
                LastRequestedMode = GameShellMode.GamingProfile.ToString(),
                LastResult = "Success"
            },
            session);

        var result = await service.ReconcileAsync(
            Settings(work),
            explicitGamingStartup: false);

        Assert.Null(result);
        Assert.Equal(0, session.WorkCalls);
    }

    [Fact]
    public async Task StartupConvergenceVerifiesWorkTopologyAcrossTwoStableSnapshots()
    {
        var work = Snapshot(
            Path("DELL", "PATH-DELL", 0, 0, 1920, 1080),
            Path("PL", "PATH-PL", 1920, 0, 3440, 1440, isPrimary: true));
        var stale = Snapshot(Path("S90F", "PATH-TV", 0, 0, 3840, 2160, isPrimary: true));
        var query = new SequenceDisplayQueryService(stale, work, work);
        var session = new FakeSessionManager();
        var service = CreateService(query, new RuntimeState { CurrentMode = GameShellMode.Work }, session);

        var result = await service.ReconcileAsync(Settings(work), explicitGamingStartup: false);

        Assert.Null(result);
        Assert.Equal(1, session.WorkCalls);
        Assert.Equal(3, query.CaptureCount);
    }

    [Fact]
    public async Task StartupConvergenceRestoresAgainWhenBootTopologyChangesAfterFirstRestore()
    {
        var work = Snapshot(
            Path("DELL", "PATH-DELL", 0, 0, 1920, 1080),
            Path("PL", "PATH-PL", 1920, 0, 3440, 1440, isPrimary: true));
        var stale = Snapshot(Path("S90F", "PATH-TV", 0, 0, 3840, 2160, isPrimary: true));
        var query = new SequenceDisplayQueryService(stale, stale, work, work);
        var session = new FakeSessionManager();
        var service = CreateService(query, new RuntimeState { CurrentMode = GameShellMode.Work }, session);

        var result = await service.ReconcileAsync(Settings(work), explicitGamingStartup: false);

        Assert.Null(result);
        Assert.Equal(2, session.WorkCalls);
        Assert.Equal(4, query.CaptureCount);
    }

    [Fact]
    public async Task StartupConvergenceTimesOutWithBoundedRestoreAttempts()
    {
        var work = Snapshot(
            Path("DELL", "PATH-DELL", 0, 0, 1920, 1080),
            Path("PL", "PATH-PL", 1920, 0, 3440, 1440, isPrimary: true));
        var stale = Snapshot(Path("S90F", "PATH-TV", 0, 0, 3840, 2160, isPrimary: true));
        var query = new SequenceDisplayQueryService(stale);
        var session = new FakeSessionManager();
        var service = CreateService(query, new RuntimeState { CurrentMode = GameShellMode.Work }, session);

        var result = await service.ReconcileAsync(Settings(work), explicitGamingStartup: false);

        Assert.NotNull(result);
        Assert.Equal(ModeResultStatus.TimedOut, result.Status);
        Assert.Equal(2, session.WorkCalls);
        Assert.True(query.CaptureCount <= 6);
    }

    [Fact]
    public async Task ProfileStartupTargetUsesExistingProfilePipelineWithoutRestoringWorkFirst()
    {
        var work = Snapshot(Path("DELL", "PATH-DELL", 0, 0, 1920, 1080, isPrimary: true));
        var session = new FakeSessionManager();
        var service = CreateService(work, new RuntimeState { CurrentMode = GameShellMode.Work }, session);
        var settings = Settings(work);
        settings.Display.GamingProfiles =
        [
            new DisplayLayoutProfile { Id = "profile-console", Name = "CONSOLE", Layout = work }
        ];

        var result = await service.ActivateTargetAsync(
            settings,
            StartupTarget.Profile("profile-console", "AlwaysProfile"));

        Assert.True(result?.Success, result?.Message);
        Assert.Equal(0, session.WorkCalls);
        Assert.Equal(["CONSOLE"], session.ProfileCalls);
    }

    private static StartupReconciliationService CreateService(
        DisplayLayoutSnapshot actual,
        RuntimeState runtime,
        FakeSessionManager session)
    {
        return CreateService(new FakeDisplayQueryService(actual), runtime, session);
    }

    private static StartupReconciliationService CreateService(
        IDisplayQueryService query,
        RuntimeState runtime,
        FakeSessionManager session)
    {
        return new StartupReconciliationService(
            new FakeRuntimeStateService(runtime),
            session,
            query,
            new FakeDisplayStabilizationService(),
            NullLogger<StartupReconciliationService>.Instance);
    }

    private static AppSettings Settings(DisplayLayoutSnapshot work)
    {
        return new AppSettings
        {
            WorkMode = new WorkModeSettings
            {
                DefaultDisplayLayout = work
            }
        };
    }

    private static DisplayLayoutSnapshot Snapshot(params DisplayPathSnapshot[] displays) => new()
    {
        Displays = displays
    };

    private static DisplayPathSnapshot Path(string name, string path, int x, int y, int width, int height, bool isPrimary = false) => new()
    {
        Identity = new DisplayIdentity
        {
            FriendlyName = name,
            MonitorDevicePath = path,
            EdidIdsValid = true,
            EdidManufacturerId = 1,
            EdidProductCodeId = (uint)Math.Abs(path.GetHashCode())
        },
        IsActive = true,
        IsPrimary = isPrimary,
        PositionX = x,
        PositionY = y,
        Width = width,
        Height = height,
        RefreshRateNumerator = 60000,
        RefreshRateDenominator = 1000
    };

    private sealed class FakeRuntimeStateService : IRuntimeStateService
    {
        private readonly RuntimeState _state;
        public FakeRuntimeStateService(RuntimeState state) => _state = state;
        public string StatePath => "fake-state.json";
        public Task<RuntimeState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_state);
        public Task SaveAsync(RuntimeState state, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDisplayQueryService : IDisplayQueryService
    {
        private readonly DisplayLayoutSnapshot _actual;
        public FakeDisplayQueryService(DisplayLayoutSnapshot actual) => _actual = actual;
        public Task<IReadOnlyList<DisplayDeviceInfo>> GetAvailableDisplaysAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DisplayDeviceInfo>>([]);
        public Task<DisplayLayoutSnapshot> CaptureCurrentLayoutAsync(CancellationToken cancellationToken = default) => Task.FromResult(_actual);
    }

    private sealed class SequenceDisplayQueryService : IDisplayQueryService
    {
        private readonly IReadOnlyList<DisplayLayoutSnapshot> _snapshots;
        private int _index;

        public SequenceDisplayQueryService(params DisplayLayoutSnapshot[] snapshots)
        {
            _snapshots = snapshots;
        }

        public int CaptureCount { get; private set; }

        public Task<IReadOnlyList<DisplayDeviceInfo>> GetAvailableDisplaysAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DisplayDeviceInfo>>([]);

        public Task<DisplayLayoutSnapshot> CaptureCurrentLayoutAsync(CancellationToken cancellationToken = default)
        {
            CaptureCount++;
            var snapshot = _snapshots[Math.Min(_index++, _snapshots.Count - 1)];
            return Task.FromResult(snapshot);
        }
    }

    private sealed class FakeDisplayStabilizationService : IDisplayDiscoveryStabilizationService
    {
        public DisplayDiscoveryState State => DisplayDiscoveryState.Ready;
        public Task WaitForSettledAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureStableAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSessionManager : IProfileSessionManager
    {
        public int WorkCalls { get; private set; }
        public List<string> ProfileCalls { get; } = [];
        public ProfileSessionSnapshot GetSnapshot() => new();
        public Task<ModeResult> EnterSelectedProfileAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ModeResult { Status = ModeResultStatus.Success, CurrentState = ModeState.GameShell });
        public Task<ModeResult> EnterProfileAsync(string profileName, CancellationToken cancellationToken = default)
        {
            ProfileCalls.Add(profileName);
            return Task.FromResult(new ModeResult { Status = ModeResultStatus.Success, CurrentState = ModeState.GameShell });
        }
        public Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
        {
            WorkCalls++;
            return Task.FromResult(new ModeResult { Status = ModeResultStatus.Success, CurrentState = ModeState.WorkMode });
        }
    }
}
