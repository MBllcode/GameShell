using GameShell.Core.Displays;
using GameShell.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class GamingProfileTopologyReadinessTests
{
    [Fact]
    public async Task Saved144_Current144_IsReady()
    {
        var result = await EvaluateAsync(144, 1, Mode(3840, 2160, 144, 1));

        AssertTarget(result, ProfileDisplayReadiness.Ready, ProfileDisplayReadinessReason.None);
    }

    [Fact]
    public async Task Saved60_Current60_IsReady()
    {
        var result = await EvaluateAsync(60, 1, Mode(3840, 2160, 60, 1));

        AssertTarget(result, ProfileDisplayReadiness.Ready, ProfileDisplayReadinessReason.None);
    }

    [Fact]
    public async Task InvalidInventory_IsNotClassifiedAsExpectedModeUnavailable()
    {
        var query = new FakeDisplayQuery([Device("S90F", 7)], _ => [Mode(3440, 1440, 165, 1)], inventoryValid: false);
        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", new GamingProfileDisplayTopology { Displays = [Target(refreshNumerator: 60, refreshDenominator: 1)] });

        AssertTarget(result, ProfileDisplayReadiness.InvalidConfiguration, ProfileDisplayReadinessReason.ModeInventoryInvalid);
    }

    [Fact]
    public async Task Saved144_Current60_IsDegraded()
    {
        var result = await EvaluateAsync(144, 1, Mode(3840, 2160, 60, 1));

        AssertTarget(result, ProfileDisplayReadiness.Degraded, ProfileDisplayReadinessReason.ExpectedModeUnavailable);
    }

    [Fact]
    public async Task UnknownSavedRefresh_IsInvalidConfiguration_NotDegraded()
    {
        var target = AutoTarget();
        target.RefreshPolicy = RefreshPolicy.Fixed;
        var query = new FakeDisplayQuery([Device("S90F", 7)], _ => [Mode(3840, 2160, 144, 1)]);
        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", new GamingProfileDisplayTopology { Displays = [target] });

        AssertTarget(result, ProfileDisplayReadiness.InvalidConfiguration, ProfileDisplayReadinessReason.InvalidExpectedMode);
    }

    [Fact]
    public async Task Saved120_Current119Point88_IsReady()
    {
        var result = await EvaluateAsync(120, 1, Mode(3840, 2160, 11988, 100));

        AssertTarget(result, ProfileDisplayReadiness.Ready, ProfileDisplayReadinessReason.None);
    }

    [Fact]
    public async Task AutoHighest_WithModes60_120_144_IsReady()
    {
        var topology = new GamingProfileDisplayTopology { Displays = [AutoTarget()] };
        var query = new FakeDisplayQuery([Device("S90F", 7)], _ => [Mode(3840, 2160, 60, 1), Mode(3840, 2160, 120, 1), Mode(3840, 2160, 144, 1)]);

        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", topology);

        AssertTarget(result, ProfileDisplayReadiness.Ready, ProfileDisplayReadinessReason.None);
        Assert.Equal(3, result.Targets[0].AvailableModesForRequestedResolution);
    }

    [Fact]
    public async Task AutoHighest_WithOnly60_IsReady()
    {
        var topology = new GamingProfileDisplayTopology { Displays = [AutoTarget()] };
        var query = new FakeDisplayQuery([Device("S90F", 7)], _ => [Mode(3840, 2160, 60, 1)]);

        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", topology);

        AssertTarget(result, ProfileDisplayReadiness.Ready, ProfileDisplayReadinessReason.None);
    }

    [Fact]
    public async Task AutoHighest_InactiveTarget_DoesNotRequireModeInventoryDuringPrecheck()
    {
        var query = new FakeDisplayQuery([Device("S90F", 7, isEnabled: false)], _ => throw new InvalidOperationException("inactive target must not query modes"));
        var topology = new GamingProfileDisplayTopology { Displays = [AutoTarget()] };

        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", topology, deferInactiveModeValidation: true);

        AssertTarget(result, ProfileDisplayReadiness.ResolvableInactive, ProfileDisplayReadinessReason.TargetInactive);
    }

    [Fact]
    public async Task AutoHighest_InactiveTarget_WithBestKnownDoesNotBlockOnHistoricalRefresh()
    {
        var query = new FakeDisplayQuery([Device("Generic", 7, isEnabled: false)], _ => throw new InvalidOperationException("inactive target must not query modes"));
        var topology = new GamingProfileDisplayTopology { Displays = [AutoTarget()] };
        var displaySettings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay
                {
                    PhysicalIdentity = Identity("S90F", 7),
                    CanonicalFriendlyName = "S90F",
                    BestKnownCapability = new DisplayCapabilitySnapshot
                    {
                        Width = 3840,
                        Height = 2160,
                        MaxRefreshRateNumerator = 144,
                        MaxRefreshRateDenominator = 1
                    }
                }
            ]
        };

        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", topology, deferInactiveModeValidation: true, displaySettings: displaySettings);

        AssertTarget(result, ProfileDisplayReadiness.ResolvableInactive, ProfileDisplayReadinessReason.TargetInactive);
        Assert.Equal(144, result.Targets[0].BestKnownCapability!.MaxRefreshRateHz);
    }

    [Fact]
    public async Task InactiveObservationWithChangedAdapterLuidResolvesThroughKnownRouteHistory()
    {
        var savedIdentity = Identity("S90F", 4352) with
        {
            AdapterLuid = 65578,
            MonitorDevicePath = "PATH-A"
        };
        var currentIdentity = savedIdentity with
        {
            FriendlyName = "SAMSUNG",
            AdapterLuid = 65813,
            MonitorDevicePath = string.Empty,
            EdidIdsValid = false,
            EdidManufacturerId = 0,
            EdidProductCodeId = 0,
            ConnectorInstance = 0
        };
        var known = new KnownPhysicalDisplay
        {
            Id = "known-tv",
            PhysicalIdentity = savedIdentity,
            CanonicalFriendlyName = "S90F",
            HistoricalIdentities = [savedIdentity with { AdapterLuid = 65813, MonitorDevicePath = "PATH-A" }],
            BestKnownCapability = new DisplayCapabilitySnapshot
            {
                Width = 3840,
                Height = 2160,
                MaxRefreshRateNumerator = 144,
                MaxRefreshRateDenominator = 1
            }
        };
        var settings = new DisplaySettings { KnownPhysicalDisplays = [known] };
        var target = AutoTarget();
        target.Identity = savedIdentity;
        target.KnownPhysicalDisplayId = known.Id;
        var current = new DisplayDeviceInfo
        {
            Name = "SAMSUNG",
            Identity = currentIdentity,
            TargetId = 4352,
            AdapterLuid = 65813,
            IsEnabled = false,
            IsAvailable = true
        };
        var query = new FakeDisplayQuery([current], _ => throw new InvalidOperationException("inactive target must not query modes"));

        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync(
                "KONSOLA",
                "KONSOLA",
                new GamingProfileDisplayTopology { Displays = [target] },
                deferInactiveModeValidation: true,
                displaySettings: settings);

        AssertTarget(result, ProfileDisplayReadiness.ResolvableInactive, ProfileDisplayReadinessReason.TargetInactive);
        Assert.Equal(DisplayIdentityMatchConfidence.HistoricalRouteAlias, result.Targets[0].PhysicalMatchConfidence);
        Assert.True(result.IdentityCatalogChanged);
    }

    [Fact]
    public async Task StableKnownDisplayUsesCurrentObservationRouteAfterHdmiReplug()
    {
        var savedIdentity = Identity("S90F", 4352) with
        {
            AdapterLuid = 65578,
            MonitorDevicePath = "DISPLAY-PATH-S90F",
            EdidManufacturerId = 11596,
            EdidProductCodeId = 30903
        };
        var currentIdentity = savedIdentity with
        {
            AdapterLuid = 72672,
            FriendlyName = "S90F"
        };
        var known = new KnownPhysicalDisplay
        {
            Id = "3286c1e7a95e4a26aefd091848d0265d",
            PhysicalIdentity = savedIdentity,
            CanonicalFriendlyName = "S90F",
            BestKnownCapability = new DisplayCapabilitySnapshot
            {
                Width = 3840,
                Height = 2160,
                MaxRefreshRateNumerator = 144,
                MaxRefreshRateDenominator = 1
            }
        };
        var settings = new DisplaySettings { KnownPhysicalDisplays = [known] };
        var target = AutoTarget();
        target.Identity = savedIdentity;
        target.KnownPhysicalDisplayId = known.Id;

        var stableLogicalDisplay = new DisplayDeviceInfo
        {
            Id = "logical-s90f",
            KnownPhysicalDisplayId = known.Id,
            Name = "S90F",
            IsEnabled = false,
            IsAvailable = true,
            Identity = savedIdentity,
            AdapterLuid = currentIdentity.AdapterLuid,
            TargetId = currentIdentity.TargetId,
            CurrentObservation = new CurrentDisplayObservation
            {
                Identity = currentIdentity,
                CurrentObservedName = "S90F",
                CurrentSourceDeviceName = "\\\\.\\DISPLAY1",
                CurrentConnectionTechnology = "HDMI",
                IsActive = false,
                IsAvailable = true,
                EnumeratedModes = []
            }
        };
        var query = new FakeDisplayQuery([], _ => throw new InvalidOperationException("inactive target must not query modes"));
        var discovery = new FakeStableDiscovery([stableLogicalDisplay]);

        var result = await new GamingProfileTopologyReadinessEvaluator(
                query,
                NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance,
                discovery)
            .EvaluateAsync(
                "CONSOLE",
                "CONSOLE",
                new GamingProfileDisplayTopology { Displays = [target] },
                deferInactiveModeValidation: true,
                displaySettings: settings);

        AssertTarget(result, ProfileDisplayReadiness.ResolvableInactive, ProfileDisplayReadinessReason.TargetInactive);
        Assert.Equal(known.Id, target.KnownPhysicalDisplayId);
        Assert.Equal(72672, result.Targets[0].ResolvedTarget!.Identity.AdapterLuid);
        Assert.Equal(DisplayIdentityMatchConfidence.ExactMonitorDevicePath, result.Targets[0].PhysicalMatchConfidence);
        Assert.Equal(144, result.Targets[0].BestKnownCapability!.MaxRefreshRateHz);
        Assert.Equal(known.Id, settings.KnownPhysicalDisplays[0].Id);
    }

    [Fact]
    public async Task Fixed144_WithOnly60_IsUnavailable()
    {
        var target = AutoTarget();
        target.RefreshPolicy = RefreshPolicy.Fixed;
        target.FixedRefreshRateNumerator = 144;
        target.FixedRefreshRateDenominator = 1;
        var query = new FakeDisplayQuery([Device("S90F", 7)], _ => [Mode(3840, 2160, 60, 1)]);

        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", new GamingProfileDisplayTopology { Displays = [target] });

        AssertTarget(result, ProfileDisplayReadiness.Degraded, ProfileDisplayReadinessReason.ExpectedModeUnavailable);
    }

    [Fact]
    public async Task MissingTarget_IsMissing()
    {
        var result = await EvaluateAsync(144, 1);

        AssertTarget(result, ProfileDisplayReadiness.Missing, ProfileDisplayReadinessReason.PhysicalTargetMissing);
    }

    [Fact]
    public async Task DisabledTarget_IsDisabledAndDoesNotBlock()
    {
        var query = new FakeDisplayQuery([], _ => throw new InvalidOperationException("disabled target must not query modes"));
        var topology = new GamingProfileDisplayTopology { Displays = [Target(enabled: false)] };
        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", topology);

        AssertTarget(result, ProfileDisplayReadiness.Disabled, ProfileDisplayReadinessReason.None);
        Assert.True(result.IsReady);
    }

    [Fact]
    public async Task OneReadyAndOneDegraded_BlocksProfile()
    {
        var first = Device("S90F", 7);
        var second = Device("LG", 8);
        var query = new FakeDisplayQuery([first, second], device => device.TargetId == 7
            ? [Mode(3840, 2160, 144, 1)]
            : [Mode(2560, 1440, 60, 1)]);
        var topology = new GamingProfileDisplayTopology
        {
            Displays = [Target("S90F", 7), Target("LG", 8, width: 3840, height: 2160)]
        };

        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", topology);

        Assert.False(result.IsReady);
        Assert.Equal(ProfileDisplayReadiness.Degraded, result.Targets[1].Result);
    }

    [Fact]
    public async Task FriendlyNameUnchangedButCapabilityDegraded_IsDegraded()
    {
        var result = await EvaluateAsync(144, 1, [Mode(3840, 2160, 60, 1)], savedName: "S90F", currentName: "S90F");

        AssertTarget(result, ProfileDisplayReadiness.Degraded, ProfileDisplayReadinessReason.ExpectedModeUnavailable);
    }

    [Fact]
    public async Task FriendlyNameChangedWithStrongIdentity_IsResolved()
    {
        var result = await EvaluateAsync(144, 1, [Mode(3840, 2160, 60, 1)], savedName: "S90F", currentName: "SAMSUNG");

        Assert.Equal("SAMSUNG", result.Targets[0].ResolvedTarget!.Name);
        Assert.Equal(ProfileDisplayReadiness.Degraded, result.Targets[0].Result);
    }

    [Fact]
    public async Task AutoHighestBelowBestKnown_IsReadyAndOnlyDiagnostic()
    {
        var target = Target("Model X", 10);
        target.RefreshPolicy = RefreshPolicy.AutoHighest;
        target.RefreshRateNumerator = 0;
        target.RefreshRateDenominator = 0;
        target.CapabilityExpectation = new DisplayCapabilityExpectation
        {
            Width = 3840,
            Height = 2160,
            LastKnownGoodMaxRefreshNumerator = 144,
            LastKnownGoodMaxRefreshDenominator = 1
        };
        var query = new FakeDisplayQuery([Device("Generic Manufacturer", 10)], _ => [Mode(3840, 2160, 60, 1)]);
        var displaySettings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay
                {
                    PhysicalIdentity = target.Identity,
                    CanonicalFriendlyName = "Model X",
                    BestKnownCapability = new DisplayCapabilitySnapshot
                    {
                        Width = 3840,
                        Height = 2160,
                        MaxRefreshRateNumerator = 144,
                        MaxRefreshRateDenominator = 1
                    }
                }
            ]
        };

        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("PROFILE", "PROFILE", new GamingProfileDisplayTopology { Displays = [target] }, displaySettings: displaySettings);

        Assert.True(result.Targets[0].ResolvedTarget is not null);
        Assert.Equal(ProfileDisplayReadiness.Ready, result.Targets[0].Result);
        Assert.Equal(ProfileDisplayReadinessReason.CapabilityBelowBestKnown, result.Targets[0].Reason);
        Assert.Equal("Generic Manufacturer", result.Targets[0].ResolvedTarget!.Name);
    }

    [Fact]
    public async Task SamePhysicalRouteAfterCapabilityRecovery_IsReady()
    {
        var target = Target("Model X", 10);
        target.RefreshPolicy = RefreshPolicy.AutoHighest;
        target.RefreshRateNumerator = 0;
        target.RefreshRateDenominator = 0;
        target.CapabilityExpectation = new DisplayCapabilityExpectation
        {
            Width = 3840,
            Height = 2160,
            LastKnownGoodMaxRefreshNumerator = 144,
            LastKnownGoodMaxRefreshDenominator = 1
        };
        var query = new FakeDisplayQuery([Device("Generic Manufacturer", 10)], _ => [Mode(3840, 2160, 144, 1)]);

        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("PROFILE", "PROFILE", new GamingProfileDisplayTopology { Displays = [target] });

        AssertTarget(result, ProfileDisplayReadiness.Ready, ProfileDisplayReadinessReason.None);
    }

    [Fact]
    public async Task EqualStableCandidates_AreAmbiguous()
    {
        var query = new FakeDisplayQuery([Device("A", 7), Device("B", 7)], _ => [Mode(3840, 2160, 144, 1)]);
        var result = await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", new GamingProfileDisplayTopology { Displays = [Target("Saved", 7)] });

        AssertTarget(result, ProfileDisplayReadiness.Ambiguous, ProfileDisplayReadinessReason.AmbiguousIdentity);
    }

    private static async Task<GamingProfileTopologyReadiness> EvaluateAsync(
        uint refreshNumerator,
        uint refreshDenominator,
        params DisplayModeInfo[] modes)
    {
        return await EvaluateAsync(refreshNumerator, refreshDenominator, modes, "S90F", "S90F");
    }

    private static async Task<GamingProfileTopologyReadiness> EvaluateAsync(
        uint refreshNumerator,
        uint refreshDenominator,
        DisplayModeInfo[] modes,
        string savedName,
        string currentName)
    {
        var query = new FakeDisplayQuery(modes.Length == 0 ? [] : [Device(currentName, 7)], _ => modes);
        var topology = new GamingProfileDisplayTopology { Displays = [Target(savedName, 7, refreshNumerator, refreshDenominator)] };
        return await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("KONSOLA", "KONSOLA", topology);
    }

    private static ProfileDisplayTarget Target(string name = "S90F", uint targetId = 7, uint refreshNumerator = 144, uint refreshDenominator = 1, bool enabled = true, int width = 3840, int height = 2160)
    {
        return new ProfileDisplayTarget
        {
            Identity = Identity(name, targetId),
            Enabled = enabled,
            IsPrimary = true,
            Width = width,
            Height = height,
            RefreshRateNumerator = refreshNumerator,
            RefreshRateDenominator = refreshDenominator
        };
    }

    private static ProfileDisplayTarget AutoTarget()
    {
        return new ProfileDisplayTarget
        {
            Identity = Identity("S90F", 7),
            Enabled = true,
            IsPrimary = true,
            Width = 3840,
            Height = 2160,
            RefreshPolicy = RefreshPolicy.AutoHighest
        };
    }

    private static DisplayDeviceInfo Device(string name, uint targetId, bool isEnabled = true)
    {
        return new DisplayDeviceInfo
        {
            Id = name,
            Name = name,
            Identity = Identity(name, targetId),
            SourceDeviceName = $"\\\\.\\DISPLAY{targetId}",
            TargetId = targetId,
            AdapterLuid = 1,
            IsEnabled = isEnabled,
            IsAvailable = true
        };
    }

    private static DisplayIdentity Identity(string name, uint targetId)
    {
        return new DisplayIdentity
        {
            FriendlyName = name,
            MonitorDevicePath = $"PATH-{name}",
            EdidIdsValid = true,
            AdapterLuid = 1,
            TargetId = targetId,
            EdidManufacturerId = 123,
            EdidProductCodeId = 456,
            ConnectorInstance = targetId
        };
    }

    private static DisplayModeInfo Mode(int width, int height, uint numerator, uint denominator)
    {
        return new DisplayModeInfo { Width = width, Height = height, RefreshRateNumerator = numerator, RefreshRateDenominator = denominator };
    }

    private static void AssertTarget(GamingProfileTopologyReadiness result, ProfileDisplayReadiness readiness, ProfileDisplayReadinessReason reason)
    {
        Assert.Equal(readiness, Assert.Single(result.Targets).Result);
        Assert.Equal(reason, result.Targets[0].Reason);
    }

    private sealed class FakeDisplayQuery : IDisplayQueryService
    {
        private readonly IReadOnlyList<DisplayDeviceInfo> _devices;
        private readonly Func<DisplayIdentity, IReadOnlyList<DisplayModeInfo>> _modes;
        private readonly bool _inventoryValid;

        public FakeDisplayQuery(IReadOnlyList<DisplayDeviceInfo> devices, Func<DisplayIdentity, IReadOnlyList<DisplayModeInfo>> modes, bool inventoryValid = true)
        {
            _devices = devices;
            _modes = modes;
            _inventoryValid = inventoryValid;
        }

        public Task<IReadOnlyList<DisplayDeviceInfo>> GetAvailableDisplaysAsync(CancellationToken cancellationToken = default) => Task.FromResult(_devices);
        public Task<DisplayLayoutSnapshot> CaptureCurrentLayoutAsync(CancellationToken cancellationToken = default) => Task.FromResult(new DisplayLayoutSnapshot());
        public Task<DisplayModeQueryResult> GetAvailableDisplayModesAsync(DisplayIdentity display, CancellationToken cancellationToken = default) => Task.FromResult(new DisplayModeQueryResult { Success = true, InventoryValid = _inventoryValid, Modes = _modes(display) });
    }

    private sealed class FakeStableDiscovery : IDisplayDiscoveryStabilizationService
    {
        private readonly IReadOnlyList<DisplayDeviceInfo> _displays;

        public FakeStableDiscovery(IReadOnlyList<DisplayDeviceInfo> displays)
        {
            _displays = displays;
        }

        public DisplayDiscoveryState State => DisplayDiscoveryState.Ready;

        public Task WaitForSettledAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<DisplayDeviceInfo>> GetStableDisplaysAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_displays);
    }
}
