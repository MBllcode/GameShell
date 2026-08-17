using GameShell.Core.Models;
using GameShell.Core.Services;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class KnownPhysicalDisplayCatalogTests
{
    [Fact]
    public void Degraded_observation_keeps_canonical_name_and_last_known_good_capability()
    {
        var canonical = CreateIdentity("S90F");
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay
                {
                    PhysicalIdentity = canonical,
                    CanonicalFriendlyName = "S90F",
                    BestKnownCapability = Capability(3840, 2160, 144)
                }
            ]
        };

        var result = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay("SAMSUNG", 60)]);
        var display = Assert.Single(result.Displays);

        Assert.Equal("S90F", display.Name);
        Assert.Equal("S90F", display.Identity.FriendlyName);
        Assert.Equal(DisplayCatalogState.Degraded, display.CatalogState);
        Assert.Equal(DisplayCapabilityState.Available, display.CurrentObservation?.CapabilityState);
        Assert.Equal("SAMSUNG", display.CurrentObservation?.CurrentObservedName);
        Assert.Equal("SAMSUNG", display.CurrentObservation?.CurrentFriendlyName);
        Assert.Equal(60, display.CurrentObservation?.CurrentCapability?.MaxRefreshRateHz);
        Assert.Equal(144, display.LastKnownGoodCapability?.MaxRefreshRateHz);
        Assert.Equal(144, settings.KnownPhysicalDisplays[0].BestKnownCapability?.MaxRefreshRateHz);
    }

    [Fact]
    public void Inactive_lower_observation_is_neutral_and_does_not_replace_verified_active_capability()
    {
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay
                {
                    PhysicalIdentity = CreateIdentity("S90F"),
                    CanonicalFriendlyName = "S90F",
                    BestKnownCapability = Capability(3840, 2160, 144),
                    LastVerifiedActiveCapability = Capability(3840, 2160, 144),
                    CapabilityVerificationSource = "ActiveRuntime"
                }
            ]
        };

        var result = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay("SAMSUNG", 60, isEnabled: false)]);
        var display = Assert.Single(result.Displays);

        Assert.Equal(DisplayCatalogState.InactiveObservation, display.CatalogState);
        Assert.Equal(DisplayCapabilityState.UnknownUntilActive, display.CurrentObservation?.CapabilityState);
        Assert.Equal(144, display.LastVerifiedActiveCapability?.MaxRefreshRateHz);
        Assert.Equal(60, display.CurrentObservation?.CurrentCapability?.MaxRefreshRateHz);
        Assert.Equal(144, settings.KnownPhysicalDisplays[0].BestKnownCapability?.MaxRefreshRateHz);
    }

    [Fact]
    public void First_observed_name_is_stable_when_later_observation_has_another_name()
    {
        var settings = new DisplaySettings();

        _ = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay("SAMSUNG", 60)]);
        var later = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay("S90F", 144)]);

        Assert.Equal("SAMSUNG", settings.KnownPhysicalDisplays.Single().CanonicalFriendlyName);
        Assert.Equal("SAMSUNG", Assert.Single(later.Displays).Name);
        Assert.Equal("S90F", Assert.Single(later.Displays).CurrentObservation?.CurrentObservedName);
    }

    [Fact]
    public void First_observed_name_remains_S90F_when_later_observation_is_SAMSUNG()
    {
        var settings = new DisplaySettings();

        _ = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay("S90F", 144)]);
        var later = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay("SAMSUNG", 60)]);

        Assert.Equal("S90F", settings.KnownPhysicalDisplays.Single().CanonicalFriendlyName);
        Assert.Equal("S90F", Assert.Single(later.Displays).Name);
        Assert.Equal("SAMSUNG", Assert.Single(later.Displays).CurrentObservation?.CurrentObservedName);
    }

    [Fact]
    public void Capability_upgrade_does_not_change_canonical_name()
    {
        var settings = new DisplaySettings();

        _ = KnownPhysicalDisplayCatalogService.RecordHealthyObservation(
            settings,
            CreateIdentity("SAMSUNG"),
            Capability(3840, 2160, 60),
            out _);
        _ = KnownPhysicalDisplayCatalogService.RecordHealthyObservation(
            settings,
            CreateIdentity("S90F"),
            Capability(3840, 2160, 144),
            out _);

        var known = Assert.Single(settings.KnownPhysicalDisplays);
        Assert.Equal("SAMSUNG", known.CanonicalFriendlyName);
        Assert.Equal(144, known.BestKnownCapability?.MaxRefreshRateHz);
    }

    [Fact]
    public void Profiles_sharing_a_physical_identity_resolve_to_the_same_canonical_name()
    {
        var settings = new DisplaySettings();
        _ = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay("SAMSUNG", 60)]);

        settings.GamingProfiles =
        [
            ProfileWithDisplay("A", "S90F"),
            ProfileWithDisplay("B", "SAMSUNG")
        ];

        Assert.True(KnownPhysicalDisplayCatalogService.NormalizeCanonicalNames(settings));
        var knownId = Assert.Single(settings.KnownPhysicalDisplays).Id;
        Assert.All(
            settings.GamingProfiles,
            profile =>
            {
                var target = Assert.Single(profile.DisplayTopology!.Displays);
                Assert.Equal("SAMSUNG", target.Identity.FriendlyName);
                Assert.Equal(knownId, target.KnownPhysicalDisplayId);
            });

        Assert.Equal(
            "SAMSUNG",
            KnownPhysicalDisplayCatalogService.ResolveCanonicalName(settings, CreateIdentity("S90F")));
        Assert.Equal(
            "SAMSUNG",
            KnownPhysicalDisplayCatalogService.ResolveCanonicalName(settings, CreateIdentity("SAMSUNG")));
    }

    [Fact]
    public void Profile_assignment_is_normalized_to_the_full_known_physical_identity()
    {
        var canonicalIdentity = CreateIdentity("SAMSUNG") with
        {
            MonitorDevicePath = "CANONICAL-PATH",
            OutputTechnology = "HDMI"
        };
        var observedIdentity = CreateIdentity("S90F") with
        {
            MonitorDevicePath = "CURRENT-PATH",
            OutputTechnology = "DisplayPort"
        };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay
                {
                    PhysicalIdentity = canonicalIdentity,
                    CanonicalFriendlyName = "SAMSUNG"
                }
            ],
            GamingProfiles =
            [
                new DisplayLayoutProfile
                {
                    Name = "TEST",
                    Layout = new DisplayLayoutSnapshot
                    {
                        Displays = [new DisplayPathSnapshot { Identity = observedIdentity }]
                    },
                    DisplayTopology = new GamingProfileDisplayTopology
                    {
                        Displays = [new ProfileDisplayTarget { Identity = observedIdentity, Enabled = true }]
                    }
                }
            ]
        };

        Assert.True(KnownPhysicalDisplayCatalogService.NormalizeCanonicalNames(settings));
        var profile = Assert.Single(settings.GamingProfiles);
        Assert.Equal(canonicalIdentity, Assert.Single(profile.DisplayTopology!.Displays).Identity);
        Assert.Equal(canonicalIdentity, Assert.Single(profile.Layout.Displays).Identity);
    }

    [Fact]
    public void Missing_known_display_is_returned_as_unavailable_remembered_target()
    {
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay
                {
                    PhysicalIdentity = CreateIdentity("S90F"),
                    CanonicalFriendlyName = "S90F",
                    BestKnownCapability = Capability(3840, 2160, 144)
                }
            ]
        };

        var result = KnownPhysicalDisplayCatalogService.Merge(settings, []);
        var display = Assert.Single(result.Displays);

        Assert.Equal("S90F", display.Name);
        Assert.True(display.IsRemembered);
        Assert.Equal(DisplayCatalogState.Unavailable, display.CatalogState);
        Assert.Null(display.CurrentObservation);
        Assert.Equal(3840, display.Width);
        Assert.Equal(2160, display.Height);
        Assert.Equal(144, display.LastKnownGoodCapability?.MaxRefreshRateHz);
    }

    [Fact]
    public void Better_current_capability_promotes_last_known_good_but_lower_capability_does_not()
    {
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay
                {
                    PhysicalIdentity = CreateIdentity("Display"),
                    CanonicalFriendlyName = "Display",
                    BestKnownCapability = Capability(3840, 2160, 144)
                }
            ]
        };

        _ = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay("Display", 60)]);
        Assert.Equal(144, settings.KnownPhysicalDisplays[0].BestKnownCapability?.MaxRefreshRateHz);

        _ = KnownPhysicalDisplayCatalogService.RecordHealthyObservation(settings, CreateIdentity("Display"), Capability(3840, 2160, 165), out _);
        Assert.Equal(165, settings.KnownPhysicalDisplays[0].BestKnownCapability?.MaxRefreshRateHz);
    }

    [Fact]
    public void Same_physical_identity_produces_one_logic_display()
    {
        var settings = new DisplaySettings();
        var result = KnownPhysicalDisplayCatalogService.Merge(settings,
        [
            CreateDisplay("Display", 144),
            CreateDisplay("Display degraded", 60)
        ]);

        Assert.Single(result.Displays);
        Assert.Single(settings.KnownPhysicalDisplays);
    }

    [Fact]
    public void New_profile_reuses_the_existing_known_display_id()
    {
        var settings = new DisplaySettings();
        var first = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay("SAMSUNG", 60)]);
        var knownId = Assert.Single(settings.KnownPhysicalDisplays).Id;
        var later = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay("S90F", 30, isEnabled: false)]);

        Assert.NotEqual(string.Empty, knownId);
        var display = Assert.Single(later.Displays);
        Assert.Equal(knownId, display.KnownPhysicalDisplayId);
        Assert.Equal("SAMSUNG", display.Name);
        Assert.Equal("S90F", display.CurrentObservation?.CurrentObservedName);
        Assert.Equal(knownId, Assert.Single(first.Displays).KnownPhysicalDisplayId);
    }

    [Fact]
    public void Conflicting_edid_on_reused_route_creates_a_separate_conflict_candidate()
    {
        var knownIdentity = CreateIdentity("SAMSUNG");
        var conflictingIdentity = knownIdentity with
        {
            FriendlyName = "OTHER DISPLAY",
            EdidProductCodeId = 999
        };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [new KnownPhysicalDisplay
            {
                PhysicalIdentity = knownIdentity,
                CanonicalFriendlyName = "SAMSUNG"
            }]
        };

        var result = KnownPhysicalDisplayCatalogService.Merge(settings,
        [new DisplayDeviceInfo
        {
            Name = "OTHER DISPLAY",
            Identity = conflictingIdentity,
            IsEnabled = true,
            IsAvailable = true,
            Width = 3840,
            Height = 2160,
            RefreshRateNumerator = 60,
            RefreshRateDenominator = 1
        }]);

        Assert.Equal(2, settings.KnownPhysicalDisplays.Count);
        var conflict = Assert.Single(result.Displays, display => display.CatalogState == DisplayCatalogState.IdentityConflict);
        Assert.Equal("OTHER DISPLAY", conflict.Name);
        Assert.NotEqual(settings.KnownPhysicalDisplays[0].Id, conflict.KnownPhysicalDisplayId);
    }

    [Fact]
    public void Legacy_known_duplicates_with_same_strong_identity_collapse_idempotently()
    {
        var identity = CreateIdentity("SAMSUNG");
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay { PhysicalIdentity = identity, CanonicalFriendlyName = "SAMSUNG" },
                new KnownPhysicalDisplay { PhysicalIdentity = identity with { FriendlyName = "S90F" }, CanonicalFriendlyName = "S90F" }
            ]
        };

        Assert.True(KnownPhysicalDisplayCatalogService.MigrateLegacyProfileCapabilities(settings));
        Assert.Single(settings.KnownPhysicalDisplays);
        var id = settings.KnownPhysicalDisplays[0].Id;

        Assert.False(KnownPhysicalDisplayCatalogService.MigrateLegacyProfileCapabilities(settings));
        Assert.Equal(id, settings.KnownPhysicalDisplays[0].Id);
        Assert.Equal("SAMSUNG", settings.KnownPhysicalDisplays[0].CanonicalFriendlyName);
    }

    [Fact]
    public void Legacy_profile_target_without_capability_still_creates_a_known_display_reference()
    {
        var settings = new DisplaySettings
        {
            GamingProfiles = [ProfileWithDisplay("KONSOLA", "SAMSUNG")]
        };

        Assert.True(KnownPhysicalDisplayCatalogService.MigrateLegacyProfileCapabilities(settings));
        var known = Assert.Single(settings.KnownPhysicalDisplays);

        Assert.Equal(known.Id, Assert.Single(settings.GamingProfiles[0].DisplayTopology!.Displays).KnownPhysicalDisplayId);
        Assert.Null(known.BestKnownCapability);
    }

    private static DisplayDeviceInfo CreateDisplay(string name, uint refresh, bool isEnabled = true)
    {
        var identity = CreateIdentity(name);
        return new DisplayDeviceInfo
        {
            Name = name,
            Identity = identity,
            IsEnabled = isEnabled,
            IsAvailable = true,
            Width = 3840,
            Height = 2160,
            RefreshRateNumerator = refresh,
            RefreshRateDenominator = 1,
            TargetId = identity.TargetId,
            AdapterLuid = identity.AdapterLuid
        };
    }

    private static DisplayLayoutProfile ProfileWithDisplay(string name, string displayName)
    {
        return new DisplayLayoutProfile
        {
            Name = name,
            Layout = new DisplayLayoutSnapshot
            {
                Displays =
                [new DisplayPathSnapshot { Identity = CreateIdentity(displayName), IsActive = true }]
            },
            DisplayTopology = new GamingProfileDisplayTopology
            {
                Displays =
                [new ProfileDisplayTarget { Identity = CreateIdentity(displayName), Enabled = true }]
            }
        };
    }

    private static DisplayIdentity CreateIdentity(string friendlyName)
    {
        return new DisplayIdentity
        {
            MonitorDevicePath = "\\\\?\\DISPLAY#S90F#1",
            FriendlyName = friendlyName,
            EdidIdsValid = true,
            EdidManufacturerId = 123,
            EdidProductCodeId = 456,
            ConnectorInstance = 1,
            AdapterLuid = 42,
            TargetId = 7
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
}
