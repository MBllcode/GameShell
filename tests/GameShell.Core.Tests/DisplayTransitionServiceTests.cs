using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class DisplayTransitionServiceTests
{
    [Fact]
    public void ConsoleDisplayLayoutBuilder_BuildsSinglePrimaryDisplayAtOrigin()
    {
        var builder = new ConsoleDisplayLayoutBuilder();
        var current = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));

        var result = builder.Build(current, current.Displays[1].Identity);

        var display = Assert.Single(result.Displays);
        Assert.True(display.IsActive);
        Assert.True(display.IsPrimary);
        Assert.Equal(0, display.PositionX);
        Assert.Equal(0, display.PositionY);
        Assert.Equal(2560, display.Width);
        Assert.Equal(1440, display.Height);
    }

    [Fact]
    public async Task EnterConsoleMode_ValidatesBeforeApplyAndStoresWorkReturnLayout()
    {
        var work = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        var query = new FakeDisplayQueryService(work);
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.Work });
        var applier = new FakeDisplayLayoutApplicationService(query);
        var service = CreateService(query, applier, runtime);

        var result = await service.EnterConsoleModeAsync(work.Displays[1].Identity);

        Assert.True(result.Success);
        Assert.Equal(1, applier.ValidateCount);
        Assert.Equal(1, applier.ApplyCount);
        Assert.Equal(GameShellMode.Console, runtime.State.CurrentMode);
        Assert.Same(work, runtime.State.WorkReturnLayout);
        Assert.Same(work, runtime.State.PreviousDisplayLayout);
    }

    [Fact]
    public async Task EnterConsoleMode_RollsBackAndDoesNotChangeModeWhenApplyFails()
    {
        var work = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        var query = new FakeDisplayQueryService(work);
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.Work });
        var applier = new FakeDisplayLayoutApplicationService(query) { FailApplyOnFirstCall = true };
        var service = CreateService(query, applier, runtime);

        var result = await service.EnterConsoleModeAsync(work.Displays[1].Identity);

        Assert.False(result.Success);
        Assert.True(result.RolledBack);
        Assert.Equal(GameShellMode.Work, runtime.State.CurrentMode);
        Assert.Same(work, runtime.State.WorkReturnLayout);
        Assert.Equal(2, applier.ApplyCount);
    }

    [Fact]
    public async Task EnterGamingMode_FallsBackToConsoleWhenLayoutIsMissing()
    {
        var work = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        var query = new FakeDisplayQueryService(work);
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.Work });
        var applier = new FakeDisplayLayoutApplicationService(query);
        var service = CreateService(query, applier, runtime);

        var result = await service.EnterGamingModeAsync(null, work.Displays[1].Identity);

        Assert.True(result.Success);
        Assert.Equal(GameShellMode.Console, runtime.State.CurrentMode);
        Assert.Single(query.Current.Displays);
    }

    [Fact]
    public async Task EnterWorkMode_RestoresWorkReturnLayoutAfterGamingChain()
    {
        var work = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        var gaming = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", -2560, 0, 2560, 1440));
        var query = new FakeDisplayQueryService(work);
        var runtime = new FakeRuntimeStateService(new RuntimeState { CurrentMode = GameShellMode.Work });
        var applier = new FakeDisplayLayoutApplicationService(query);
        var service = CreateService(query, applier, runtime);

        Assert.True((await service.EnterGamingModeAsync(gaming, work.Displays[1].Identity)).Success);
        Assert.True((await service.EnterConsoleModeAsync(work.Displays[1].Identity)).Success);
        Assert.True((await service.EnterWorkModeAsync()).Success);

        Assert.Equal(GameShellMode.Work, runtime.State.CurrentMode);
        Assert.True(DisplayLayoutVerifier.MatchesSnapshot(work, query.Current));
    }

    [Fact]
    public async Task EnterWorkMode_AppliesLayoutWhenOnlyPrimaryDisplayDiffers()
    {
        var work = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        var gaming = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440, isPrimary: true));
        var query = new FakeDisplayQueryService(gaming);
        var runtime = new FakeRuntimeStateService(new RuntimeState
        {
            CurrentMode = GameShellMode.GamingProfile,
            WorkReturnLayout = work
        });
        var applier = new FakeDisplayLayoutApplicationService(query);
        var service = CreateService(query, applier, runtime);

        var result = await service.EnterWorkModeAsync();

        Assert.True(result.Success);
        Assert.Equal(1, applier.ApplyCount);
        Assert.True(DisplayLayoutVerifier.MatchesSnapshot(work, query.Current));
    }

    [Fact]
    public async Task EnterWorkMode_FallsBackToPreviousDisplayLayoutWhenWorkReturnLayoutIsMissing()
    {
        var work = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        var console = Snapshot(Path("Monitor B", "PATH-B", 0, 0, 2560, 1440, isPrimary: true));
        var query = new FakeDisplayQueryService(console);
        var runtime = new FakeRuntimeStateService(new RuntimeState
        {
            CurrentMode = GameShellMode.Console,
            PreviousDisplayLayout = work
        });
        var applier = new FakeDisplayLayoutApplicationService(query);
        var service = CreateService(query, applier, runtime);

        var result = await service.EnterWorkModeAsync();

        Assert.True(result.Success);
        Assert.Equal(GameShellMode.Work, runtime.State.CurrentMode);
        Assert.True(DisplayLayoutVerifier.MatchesSnapshot(work, query.Current));
    }

    [Fact]
    public async Task EnterWorkMode_TriesPreviousDisplayLayoutWhenWorkReturnLayoutFails()
    {
        var staleWork = Snapshot(Path("Missing", "PATH-MISSING", 0, 0, 1024, 768, isPrimary: true));
        var work = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        var console = Snapshot(Path("Monitor B", "PATH-B", 0, 0, 2560, 1440, isPrimary: true));
        var query = new FakeDisplayQueryService(console);
        var runtime = new FakeRuntimeStateService(new RuntimeState
        {
            CurrentMode = GameShellMode.Console,
            WorkReturnLayout = staleWork,
            PreviousDisplayLayout = work
        });
        var applier = new FakeDisplayLayoutApplicationService(query) { FailUnknownDisplays = true };
        var service = CreateService(query, applier, runtime);

        var result = await service.EnterWorkModeAsync();

        Assert.True(result.Success);
        Assert.Equal(GameShellMode.Work, runtime.State.CurrentMode);
        Assert.True(DisplayLayoutVerifier.MatchesSnapshot(work, query.Current));
        Assert.Equal(2, applier.ApplyCount);
    }

    [Fact]
    public async Task DisplayTest_RestoresPendingLayoutManually()
    {
        var work = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        var query = new FakeDisplayQueryService(work);
        var runtime = new FakeRuntimeStateService(new RuntimeState());
        var applier = new FakeDisplayLayoutApplicationService(query);
        var service = CreateService(query, applier, runtime);

        Assert.True((await service.TestConsoleDisplayAsync(work.Displays[1].Identity)).Success);
        Assert.Single(query.Current.Displays);

        var restore = await service.RestorePendingTestAsync();

        Assert.True(restore.Success);
        Assert.True(DisplayLayoutVerifier.MatchesSnapshot(work, query.Current));
    }

    [Fact]
    public void MatchesSnapshot_RequiresSamePrimaryButIgnoresRefreshRateDifferences()
    {
        var expected = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        var actual = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440, isPrimary: true));
        actual = new DisplayLayoutSnapshot
        {
            Displays = actual.Displays.Select(display => new DisplayPathSnapshot
            {
                Identity = display.Identity,
                IsActive = display.IsActive,
                IsPrimary = display.IsPrimary,
                PositionX = display.PositionX,
                PositionY = display.PositionY,
                Width = display.Width,
                Height = display.Height,
                Orientation = display.Orientation,
                RefreshRateNumerator = 144000,
                RefreshRateDenominator = 1000
            }).ToList()
        };

        Assert.False(DisplayLayoutVerifier.MatchesSnapshot(expected, actual));

        actual = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        actual = new DisplayLayoutSnapshot
        {
            Displays = actual.Displays.Select(display => new DisplayPathSnapshot
            {
                Identity = display.Identity,
                IsActive = display.IsActive,
                IsPrimary = display.IsPrimary,
                PositionX = display.PositionX,
                PositionY = display.PositionY,
                Width = display.Width,
                Height = display.Height,
                Orientation = display.Orientation,
                RefreshRateNumerator = 144000,
                RefreshRateDenominator = 1000
            }).ToList()
        };

        Assert.True(DisplayLayoutVerifier.MatchesSnapshot(expected, actual));
    }

    [Fact]
    public void MatchesActiveDisplaySet_RejectsDifferentPrimary()
    {
        var expected = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440));
        var actual = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080),
            Path("Monitor B", "PATH-B", 1920, 0, 2560, 1440, isPrimary: true));

        Assert.False(DisplayLayoutVerifier.MatchesActiveDisplaySet(expected, actual));
    }

    [Fact]
    public void MatchesSnapshot_IgnoresUniformCoordinateTranslation()
    {
        var expected = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", -2560, 0, 2560, 1440),
            Path("Monitor C", "PATH-C", 1920, 0, 1920, 1080));
        var actual = Snapshot(
            Path("Monitor A", "PATH-A", 2560, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", 0, 0, 2560, 1440),
            Path("Monitor C", "PATH-C", 4480, 0, 1920, 1080));

        Assert.True(DisplayLayoutVerifier.MatchesSnapshot(expected, actual));
    }

    [Fact]
    public void MatchesActiveDisplaySet_AcceptsSameMonitorsWithDifferentGeometry()
    {
        var expected = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", -2560, 0, 2560, 1440),
            Path("Monitor C", "PATH-C", 1920, 0, 1920, 1080));
        var actual = Snapshot(
            Path("Monitor A", "PATH-A", 200, 120, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", -2300, 120, 2560, 1440),
            Path("Monitor C", "PATH-C", 2100, 120, 1920, 1080));

        Assert.True(DisplayLayoutVerifier.MatchesActiveDisplaySet(expected, actual));
    }

    [Fact]
    public void MatchesActiveDisplaySet_AcceptsExpectedMonitorsWhenExtraDisplayIsActive()
    {
        var expected = Snapshot(
            Path("Monitor A", "PATH-A", 0, 0, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", -2560, 0, 2560, 1440),
            Path("Monitor C", "PATH-C", 1920, 0, 1920, 1080));
        var actual = Snapshot(
            Path("Monitor A", "PATH-A", 10, 10, 1920, 1080, isPrimary: true),
            Path("Monitor B", "PATH-B", -2550, 10, 2560, 1440),
            Path("Monitor C", "PATH-C", 1930, 10, 1920, 1080),
            Path("Virtual Display", "PATH-VIRTUAL", 3850, 10, 1024, 768));

        Assert.True(DisplayLayoutVerifier.MatchesActiveDisplaySet(expected, actual));
    }

    private static DisplayTransitionService CreateService(
        FakeDisplayQueryService query,
        FakeDisplayLayoutApplicationService applier,
        FakeRuntimeStateService runtime)
    {
        return new DisplayTransitionService(
            query,
            applier,
            runtime,
            new ConsoleDisplayLayoutBuilder(),
            NullLogger<DisplayTransitionService>.Instance);
    }

    private static DisplayLayoutSnapshot Snapshot(params DisplayPathSnapshot[] displays)
    {
        return new DisplayLayoutSnapshot
        {
            Displays = displays,
            CapturedAt = DateTimeOffset.UtcNow
        };
    }

    private static DisplayPathSnapshot Path(string name, string path, int x, int y, int width, int height, bool isPrimary = false)
    {
        return new DisplayPathSnapshot
        {
            Identity = new DisplayIdentity
            {
                MonitorDevicePath = path,
                FriendlyName = name,
                EdidManufacturerId = 1,
                EdidProductCodeId = (uint)Math.Abs(path.GetHashCode()),
                ConnectorInstance = 1
            },
            IsActive = true,
            IsPrimary = isPrimary,
            PositionX = x,
            PositionY = y,
            Width = width,
            Height = height,
            Orientation = DisplayOrientation.Identity,
            RefreshRateNumerator = 60000,
            RefreshRateDenominator = 1000
        };
    }

    private sealed class FakeDisplayQueryService : IDisplayQueryService
    {
        public FakeDisplayQueryService(DisplayLayoutSnapshot current)
        {
            Current = current;
        }

        public DisplayLayoutSnapshot Current { get; set; }

        public Task<IReadOnlyList<DisplayDeviceInfo>> GetAvailableDisplaysAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DisplayDeviceInfo> displays = Current.Displays.Select(display => new DisplayDeviceInfo
            {
                Id = display.Identity.MonitorDevicePath,
                Name = display.Identity.FriendlyName,
                IsEnabled = display.IsActive,
                IsPrimary = display.IsPrimary,
                Identity = display.Identity,
                Width = display.Width,
                Height = display.Height,
                PositionX = display.PositionX,
                PositionY = display.PositionY,
                RefreshRateNumerator = display.RefreshRateNumerator,
                RefreshRateDenominator = display.RefreshRateDenominator
            }).ToList();

            return Task.FromResult(displays);
        }

        public Task<DisplayLayoutSnapshot> CaptureCurrentLayoutAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Current);
        }
    }

    private sealed class FakeDisplayLayoutApplicationService : IDisplayLayoutApplicationService
    {
        private readonly FakeDisplayQueryService _query;

        public FakeDisplayLayoutApplicationService(FakeDisplayQueryService query)
        {
            _query = query;
        }

        public bool FailApplyOnFirstCall { get; init; }
        public bool FailUnknownDisplays { get; init; }
        public int ValidateCount { get; private set; }
        public int ApplyCount { get; private set; }

        public Task<DisplayOperationResult> ValidateLayoutAsync(DisplayLayoutSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            ValidateCount++;
            return Task.FromResult(new DisplayOperationResult { Success = true, Message = "Valid" });
        }

        public Task<DisplayOperationResult> ApplyLayoutAsync(DisplayLayoutSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            ApplyCount++;
            if (FailApplyOnFirstCall && ApplyCount == 1)
            {
                return Task.FromResult(new DisplayOperationResult { Success = false, Message = "Apply failed" });
            }

            if (FailUnknownDisplays && snapshot.Displays.Any(display => display.Identity.MonitorDevicePath.Contains("MISSING", StringComparison.Ordinal)))
            {
                return Task.FromResult(new DisplayOperationResult { Success = false, Message = "Missing display" });
            }

            _query.Current = snapshot;
            return Task.FromResult(new DisplayOperationResult { Success = true, Message = "Applied" });
        }

        public Task<DisplayOperationResult> ActivateOnlyAsync(DisplayIdentity display, CancellationToken cancellationToken = default)
        {
            ValidateCount++;
            var selected = _query.Current.Displays.SingleOrDefault(path => DisplayIdentityMatcher.IsReliableMatch(display, path.Identity));
            if (selected is null)
            {
                return Task.FromResult(new DisplayOperationResult { Success = false, Message = "Missing display" });
            }

            return ApplyLayoutAsync(new DisplayLayoutSnapshot
            {
                Displays =
                [
                    new DisplayPathSnapshot
                    {
                        Identity = selected.Identity,
                        IsActive = true,
                        IsPrimary = true,
                        PositionX = 0,
                        PositionY = 0,
                        Width = selected.Width,
                        Height = selected.Height,
                        Orientation = selected.Orientation,
                        RefreshRateNumerator = selected.RefreshRateNumerator,
                        RefreshRateDenominator = selected.RefreshRateDenominator
                    }
                ]
            }, cancellationToken);
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
