using GameShell.Core.Displays;
using GameShell.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class DisplayCapabilityOwnershipTests
{
    [Fact]
    public void Healthy_observation_initializes_global_best_known_capability()
    {
        var settings = new DisplaySettings();

        var changed = KnownPhysicalDisplayCatalogService.RecordHealthyObservation(
            settings,
            Identity("S90F"),
            Capability(3840, 2160, 144),
            out var update);

        Assert.True(changed);
        Assert.Equal("Initialized", update.Action);
        Assert.Equal(144, Assert.Single(settings.KnownPhysicalDisplays).BestKnownCapability!.MaxRefreshRateHz);
    }

    [Fact]
    public void Degraded_second_profile_cannot_write_a_separate_lower_best_known_capability()
    {
        var target = Identity("S90F");
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [new KnownPhysicalDisplay { PhysicalIdentity = target, CanonicalFriendlyName = "S90F", BestKnownCapability = Capability(3840, 2160, 144) }]
        };

        var merged = KnownPhysicalDisplayCatalogService.Merge(settings, [Display("SAMSUNG", target, 60)]);
        var display = Assert.Single(merged.Displays);

        Assert.Equal(DisplayCatalogState.Degraded, display.CatalogState);
        Assert.Equal(144, settings.KnownPhysicalDisplays.Single().BestKnownCapability!.MaxRefreshRateHz);
        Assert.Single(settings.KnownPhysicalDisplays);
    }

    [Fact]
    public async Task Auto_highest_treats_global_best_known_as_diagnostic_only()
    {
        var settings = SettingsWithBestKnown(144);
        var target = Target(RefreshPolicy.AutoHighest);
        var result = await EvaluateAsync(settings, target, [Mode(3840, 2160, 60)]);

        Assert.Equal(ProfileDisplayReadiness.Ready, Assert.Single(result.Targets).Result);
        Assert.Equal(ProfileDisplayReadinessReason.CapabilityBelowBestKnown, result.Targets[0].Reason);
    }

    [Fact]
    public async Task Fixed_60_remains_ready_even_when_best_known_is_144()
    {
        var settings = SettingsWithBestKnown(144);
        var target = Target(RefreshPolicy.Fixed);
        target.FixedRefreshRateNumerator = 60;
        target.FixedRefreshRateDenominator = 1;
        var result = await EvaluateAsync(settings, target, [Mode(3840, 2160, 60)]);

        Assert.Equal(ProfileDisplayReadiness.Ready, Assert.Single(result.Targets).Result);
    }

    [Fact]
    public async Task Current_144_is_ready_for_profiles_sharing_the_catalog_entry()
    {
        var settings = SettingsWithBestKnown(144);
        var first = await EvaluateAsync(settings, Target(RefreshPolicy.AutoHighest), [Mode(3840, 2160, 144)]);
        var second = await EvaluateAsync(settings, Target(RefreshPolicy.AutoHighest), [Mode(3840, 2160, 144)]);

        Assert.True(first.IsReady);
        Assert.True(second.IsReady);
    }

    [Fact]
    public async Task Current_120_is_ready_and_does_not_lower_best_known()
    {
        var settings = SettingsWithBestKnown(144);
        var result = await EvaluateAsync(settings, Target(RefreshPolicy.AutoHighest), [Mode(3840, 2160, 120)]);

        Assert.Equal(ProfileDisplayReadiness.Ready, Assert.Single(result.Targets).Result);
        Assert.Equal(ProfileDisplayReadinessReason.CapabilityBelowBestKnown, result.Targets[0].Reason);
        Assert.Equal(144, settings.KnownPhysicalDisplays.Single().BestKnownCapability!.MaxRefreshRateHz);
    }

    [Fact]
    public void Healthy_165_can_upgrade_same_physical_catalog_entry()
    {
        var settings = SettingsWithBestKnown(144);

        var changed = KnownPhysicalDisplayCatalogService.RecordHealthyObservation(
            settings,
            Identity("SAMSUNG"),
            Capability(3840, 2160, 165),
            out var update);

        Assert.True(changed);
        Assert.Equal("Upgraded", update.Action);
        Assert.Equal(165, settings.KnownPhysicalDisplays.Single().BestKnownCapability!.MaxRefreshRateHz);
    }

    [Fact]
    public async Task Best_known_capability_survives_settings_restart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gameshell-capability-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new AppSettings
            {
                Display = SettingsWithBestKnown(144)
            };
            var writer = new SettingsService(NullLogger<SettingsService>.Instance, path);
            await writer.SaveAsync(settings);

            var reloaded = await new SettingsService(NullLogger<SettingsService>.Instance, path).LoadAsync();

            Assert.Equal(144, Assert.Single(reloaded.Display.KnownPhysicalDisplays).BestKnownCapability!.MaxRefreshRateHz);
            Assert.Equal(144, reloaded.Display.KnownPhysicalDisplays.Single().LastVerifiedActiveCapability!.MaxRefreshRateHz);
            Assert.Equal("S90F", reloaded.Display.KnownPhysicalDisplays.Single().CanonicalFriendlyName);
        }
        finally
        {
            foreach (var candidate in new[] { path, $"{path}.bak", $"{path}.tmp" })
            {
                if (File.Exists(candidate))
                {
                    File.Delete(candidate);
                }
            }
        }
    }

    [Fact]
    public void Legacy_profile_capabilities_are_grouped_and_migrated_once()
    {
        var identity = Identity("S90F");
        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                GamingProfiles =
                [
                    Profile("KONSOLA", identity, 144),
                    Profile("TEST", identity with { FriendlyName = "SAMSUNG" }, 60)
                ]
            }
        };

        AppSettingsValidator.Validate(settings);

        Assert.Equal(144, Assert.Single(settings.Display.KnownPhysicalDisplays).BestKnownCapability!.MaxRefreshRateHz);
        Assert.All(settings.Display.GamingProfiles.SelectMany(profile => profile.DisplayTopology!.Displays), target => Assert.Null(target.CapabilityExpectation));
    }

    private static async Task<GamingProfileTopologyReadiness> EvaluateAsync(
        DisplaySettings settings,
        ProfileDisplayTarget target,
        IReadOnlyList<DisplayModeInfo> modes)
    {
        var query = new FakeDisplayQuery([Display("Current", target.Identity, modes.Max(mode => mode.RefreshRateNumerator))], modes);
        return await new GamingProfileTopologyReadinessEvaluator(query, NullLogger<GamingProfileTopologyReadinessEvaluator>.Instance)
            .EvaluateAsync("PROFILE", "PROFILE", new GamingProfileDisplayTopology { Displays = [target] }, displaySettings: settings);
    }

    private static DisplaySettings SettingsWithBestKnown(uint refresh)
    {
        return new DisplaySettings
        {
            KnownPhysicalDisplays =
            [new KnownPhysicalDisplay { PhysicalIdentity = Identity("S90F"), CanonicalFriendlyName = "S90F", BestKnownCapability = Capability(3840, 2160, refresh) }]
        };
    }

    private static DisplayLayoutProfile Profile(string name, DisplayIdentity identity, uint refresh)
    {
        return new DisplayLayoutProfile
        {
            Name = name,
            DisplayTopology = new GamingProfileDisplayTopology
            {
                Displays =
                [new ProfileDisplayTarget
                {
                    Identity = identity,
                    Enabled = true,
                    IsPrimary = true,
                    Width = 3840,
                    Height = 2160,
                    RefreshPolicy = RefreshPolicy.AutoHighest,
                    CapabilityExpectation = new DisplayCapabilityExpectation
                    {
                        Width = 3840,
                        Height = 2160,
                        LastKnownGoodMaxRefreshNumerator = refresh,
                        LastKnownGoodMaxRefreshDenominator = 1
                    }
                }]
            }
        };
    }

    private static ProfileDisplayTarget Target(RefreshPolicy policy)
    {
        return new ProfileDisplayTarget
        {
            Identity = Identity("S90F"),
            Enabled = true,
            IsPrimary = true,
            Width = 3840,
            Height = 2160,
            RefreshPolicy = policy
        };
    }

    private static DisplayDeviceInfo Display(string name, DisplayIdentity identity, uint refresh)
    {
        return new DisplayDeviceInfo
        {
            Name = name,
            Identity = identity,
            IsEnabled = true,
            IsAvailable = true,
            Width = 3840,
            Height = 2160,
            RefreshRateNumerator = refresh,
            RefreshRateDenominator = 1
        };
    }

    private static DisplayIdentity Identity(string name)
    {
        return new DisplayIdentity
        {
            FriendlyName = name,
            MonitorDevicePath = "\\\\?\\DISPLAY#S90F#1",
            EdidIdsValid = true,
            EdidManufacturerId = 123,
            EdidProductCodeId = 456,
            ConnectorInstance = 1,
            AdapterLuid = 65578,
            TargetId = 4352
        };
    }

    private static DisplayCapabilitySnapshot Capability(int width, int height, uint refresh)
    {
        return new DisplayCapabilitySnapshot
        {
            Width = width,
            Height = height,
            MaxRefreshRateNumerator = refresh,
            MaxRefreshRateDenominator = 1
        };
    }

    private static DisplayModeInfo Mode(int width, int height, uint refresh)
    {
        return new DisplayModeInfo { Width = width, Height = height, RefreshRateNumerator = refresh, RefreshRateDenominator = 1 };
    }

    private sealed class FakeDisplayQuery : IDisplayQueryService
    {
        private readonly IReadOnlyList<DisplayDeviceInfo> _devices;
        private readonly IReadOnlyList<DisplayModeInfo> _modes;

        public FakeDisplayQuery(IReadOnlyList<DisplayDeviceInfo> devices, IReadOnlyList<DisplayModeInfo> modes)
        {
            _devices = devices;
            _modes = modes;
        }

        public Task<IReadOnlyList<DisplayDeviceInfo>> GetAvailableDisplaysAsync(CancellationToken cancellationToken = default) => Task.FromResult(_devices);
        public Task<DisplayLayoutSnapshot> CaptureCurrentLayoutAsync(CancellationToken cancellationToken = default) => Task.FromResult(new DisplayLayoutSnapshot());
        public Task<DisplayModeQueryResult> GetAvailableDisplayModesAsync(DisplayIdentity display, CancellationToken cancellationToken = default) => Task.FromResult(new DisplayModeQueryResult { Success = true, InventoryValid = true, Modes = _modes });
    }
}
