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

    [Fact]
    public void Changed_adapter_luid_with_same_monitor_path_reuses_known_display_and_learns_route_alias()
    {
        var saved = CreateIdentity("S90F") with
        {
            AdapterLuid = 65578,
            TargetId = 4352,
            MonitorDevicePath = "PATH-A"
        };
        var observed = saved with
        {
            FriendlyName = "S90F",
            AdapterLuid = 65813
        };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [new KnownPhysicalDisplay
            {
                Id = "known-s90f",
                PhysicalIdentity = saved,
                CanonicalFriendlyName = "S90F"
            }]
        };

        var result = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay(observed, 60)]);

        Assert.Single(result.Displays);
        Assert.Equal("known-s90f", result.Displays[0].KnownPhysicalDisplayId);
        Assert.Equal(DisplayIdentityMatchConfidence.ExactMonitorDevicePath, result.IdentityMatches.Single().MatchConfidence);
        Assert.Contains(observed, settings.KnownPhysicalDisplays.Single().HistoricalIdentities);
    }

    [Fact]
    public void Later_degraded_observation_with_missing_metadata_resolves_through_historical_route()
    {
        var saved = CreateIdentity("S90F") with
        {
            AdapterLuid = 65578,
            TargetId = 4352,
            MonitorDevicePath = "PATH-A"
        };
        var changedRoute = saved with { AdapterLuid = 65813 };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [new KnownPhysicalDisplay
            {
                PhysicalIdentity = saved,
                CanonicalFriendlyName = "S90F",
                HistoricalIdentities = [changedRoute]
            }]
        };
        var degraded = changedRoute with
        {
            FriendlyName = "SAMSUNG",
            MonitorDevicePath = string.Empty,
            EdidIdsValid = false,
            EdidManufacturerId = 0,
            EdidProductCodeId = 0,
            ConnectorInstance = 0
        };

        var match = DisplayIdentityMatcher.ResolveKnownDisplay(settings.KnownPhysicalDisplays.Single(), degraded);

        Assert.True(match.IsMatch);
        Assert.Equal(DisplayIdentityMatchConfidence.HistoricalRouteAlias, match.MatchConfidence);
        Assert.True(match.MissingMetadata);
        Assert.False(match.HasConflict);

        var result = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay(degraded, 60, isEnabled: false)]);
        Assert.Single(result.Displays);
        Assert.Equal("S90F", result.Displays[0].Name);
    }

    [Fact]
    public void Valid_conflicting_edid_does_not_merge_even_when_route_alias_exists()
    {
        var saved = CreateIdentity("S90F") with
        {
            AdapterLuid = 65578,
            TargetId = 4352,
            MonitorDevicePath = "PATH-A"
        };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [new KnownPhysicalDisplay
            {
                PhysicalIdentity = saved,
                CanonicalFriendlyName = "S90F",
                HistoricalIdentities = [saved with { AdapterLuid = 65813 }]
            }]
        };
        var conflicting = saved with
        {
            FriendlyName = "OTHER",
            AdapterLuid = 65813,
            EdidProductCodeId = 999
        };

        var match = DisplayIdentityMatcher.ResolveKnownDisplay(settings.KnownPhysicalDisplays.Single(), conflicting);

        Assert.False(match.IsMatch);
        Assert.True(match.HasConflict);
        Assert.Equal(DisplayIdentityMatchConfidence.IdentityConflict, match.MatchConfidence);
    }

    [Fact]
    public void Reconciliation_collapses_duplicate_known_display_and_repoints_profile()
    {
        var originalIdentity = CreateIdentity("S90F") with
        {
            AdapterLuid = 65578,
            TargetId = 4352,
            MonitorDevicePath = "PATH-A"
        };
        var alias = originalIdentity with { AdapterLuid = 65813 };
        var duplicate = new KnownPhysicalDisplay
        {
            Id = "duplicate",
            PhysicalIdentity = alias,
            CanonicalFriendlyName = "SAMSUNG"
        };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay
                {
                    Id = "original",
                    PhysicalIdentity = originalIdentity,
                    HistoricalIdentities = [alias],
                    CanonicalFriendlyName = "S90F"
                },
                duplicate
            ],
            GamingProfiles =
            [
                new DisplayLayoutProfile
                {
                    Name = "KONSOLA",
                    DisplayTopology = new GamingProfileDisplayTopology
                    {
                        Displays = [new ProfileDisplayTarget
                        {
                            KnownPhysicalDisplayId = "original",
                            Identity = originalIdentity,
                            Enabled = true
                        }]
                    }
                },
                new DisplayLayoutProfile
                {
                    Name = "TEST",
                    DisplayTopology = new GamingProfileDisplayTopology
                    {
                        Displays = [new ProfileDisplayTarget
                        {
                            KnownPhysicalDisplayId = "duplicate",
                            Identity = alias,
                            Enabled = true
                        }]
                    }
                }
            ]
        };

        Assert.True(KnownPhysicalDisplayCatalogService.MigrateLegacyProfileCapabilities(settings));

        Assert.Single(settings.KnownPhysicalDisplays);
        Assert.Equal("original", settings.KnownPhysicalDisplays.Single().Id);
        var consoleTarget = settings.GamingProfiles.Single(profile => profile.Name == "KONSOLA").DisplayTopology!.Displays.Single();
        var testTarget = settings.GamingProfiles.Single(profile => profile.Name == "TEST").DisplayTopology!.Displays.Single();
        Assert.Equal("original", consoleTarget.KnownPhysicalDisplayId);
        Assert.Equal("original", testTarget.KnownPhysicalDisplayId);
        Assert.Equal("S90F", testTarget.Identity.FriendlyName);
    }

    [Fact]
    public void Discovery_with_known_route_alias_keeps_three_logical_displays()
    {
        var tvSaved = CreateIdentity("S90F") with
        {
            AdapterLuid = 65578,
            TargetId = 4352,
            MonitorDevicePath = "PATH-TV"
        };
        var tvRoute = tvSaved with { AdapterLuid = 65813 };
        var dell = CreateIdentity("DELL") with
        {
            TargetId = 1,
            MonitorDevicePath = "PATH-DELL",
            EdidProductCodeId = 1001
        };
        var pl = CreateIdentity("PL3480WQ") with
        {
            TargetId = 2,
            MonitorDevicePath = "PATH-PL",
            EdidProductCodeId = 1002
        };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay { Id = "dell", PhysicalIdentity = dell, CanonicalFriendlyName = "DELL" },
                new KnownPhysicalDisplay { Id = "pl", PhysicalIdentity = pl, CanonicalFriendlyName = "PL3480WQ" },
                new KnownPhysicalDisplay { Id = "tv", PhysicalIdentity = tvSaved, HistoricalIdentities = [tvRoute], CanonicalFriendlyName = "S90F" }
            ]
        };

        var result = KnownPhysicalDisplayCatalogService.Merge(settings,
        [
            CreateDisplay(dell, 60),
            CreateDisplay(pl, 60),
            CreateDisplay(tvRoute with
            {
                FriendlyName = "SAMSUNG",
                MonitorDevicePath = string.Empty,
                EdidIdsValid = false,
                EdidManufacturerId = 0,
                EdidProductCodeId = 0,
                ConnectorInstance = 0
            }, 60, isEnabled: false)
        ]);

        Assert.Equal(3, result.LogicalCount);
        Assert.Equal(3, settings.KnownPhysicalDisplays.Count);
        Assert.Equal(3, result.MatchedCount);
        Assert.Equal(0, result.DuplicatesCollapsed);
        Assert.Contains(result.Displays, display => display.Name == "S90F" && display.KnownPhysicalDisplayId == "tv");
    }

    [Fact]
    public void Polluted_catalog_keeps_profile_survivor_when_route_and_product_metadata_changed()
    {
        var original = CreateIdentity("S90F") with
        {
            AdapterLuid = 65578,
            TargetId = 4352,
            MonitorDevicePath = @"\\?\DISPLAY#SAM78B7#5&197e7451&1&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
            EdidProductCodeId = 30903,
            OutputTechnology = "HDMI"
        };
        var duplicateIdentity = original with
        {
            FriendlyName = "SAMSUNG",
            AdapterLuid = 65813,
            MonitorDevicePath = @"\\?\DISPLAY#SAM77F2#5&197e7451&1&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
            EdidProductCodeId = 30706
        };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay
                {
                    Id = "A",
                    PhysicalIdentity = original,
                    CanonicalFriendlyName = "S90F",
                    BestKnownCapability = Capability(3840, 2160, 144)
                },
                new KnownPhysicalDisplay
                {
                    Id = "B",
                    PhysicalIdentity = duplicateIdentity,
                    CanonicalFriendlyName = "SAMSUNG"
                }
            ],
            GamingProfiles =
            [new DisplayLayoutProfile
            {
                Name = "CONSOLE",
                DisplayTopology = new GamingProfileDisplayTopology
                {
                    Displays = [new ProfileDisplayTarget
                    {
                        KnownPhysicalDisplayId = "A",
                        Identity = original,
                        Enabled = true
                    }]
                }
            }]
        };

        Assert.True(KnownPhysicalDisplayCatalogService.MigrateLegacyProfileCapabilities(settings));
        Assert.Single(settings.KnownPhysicalDisplays);
        Assert.Equal("A", settings.KnownPhysicalDisplays.Single().Id);
        Assert.Equal("S90F", settings.KnownPhysicalDisplays.Single().CanonicalFriendlyName);
        Assert.Contains(duplicateIdentity, settings.KnownPhysicalDisplays.Single().HistoricalIdentities);

        var current = KnownPhysicalDisplayCatalogService.Merge(
            settings,
            [CreateDisplay(duplicateIdentity, 60, isEnabled: false)]);

        Assert.Single(current.Displays);
        Assert.Equal("A", current.Displays.Single().KnownPhysicalDisplayId);
        Assert.Equal("S90F", current.Displays.Single().Name);
    }

    [Fact]
    public void Strong_replug_alias_wins_over_stale_conflicting_historical_alias()
    {
        var original = CreateIdentity("S90F") with
        {
            AdapterLuid = 65578,
            TargetId = 4352,
            MonitorDevicePath = @"\\?\DISPLAY#SAM78B7#5&197e7451&1&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
            EdidProductCodeId = 30903,
            OutputTechnology = "HDMI"
        };
        var degraded = original with
        {
            FriendlyName = "SAMSUNG",
            AdapterLuid = 65813,
            MonitorDevicePath = @"\\?\DISPLAY#SAM77F2#5&197e7451&1&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
            EdidProductCodeId = 30706
        };
        var replugged = original with { AdapterLuid = 65643 };
        var known = new KnownPhysicalDisplay
        {
            Id = "tv",
            PhysicalIdentity = original,
            CanonicalFriendlyName = "S90F",
            HistoricalIdentities = [degraded],
            BestKnownCapability = Capability(3840, 2160, 144)
        };

        var match = DisplayIdentityMatcher.ResolveKnownDisplay(known, replugged);

        Assert.True(match.IsMatch);
        Assert.False(match.HasConflict);
        Assert.Equal(DisplayIdentityMatchConfidence.ExactMonitorDevicePath, match.MatchConfidence);

        var settings = new DisplaySettings { KnownPhysicalDisplays = [known] };
        var result = KnownPhysicalDisplayCatalogService.Merge(settings, [CreateDisplay(replugged, 144)]);
        Assert.Equal(1, result.LogicalCount);
        Assert.Single(settings.KnownPhysicalDisplays);

        _ = KnownPhysicalDisplayCatalogService.RecordHealthyObservation(
            settings,
            replugged,
            Capability(3840, 2160, 144),
            out var update);

        Assert.Equal("Kept", update.Action);
        Assert.Equal(144, settings.KnownPhysicalDisplays.Single().BestKnownCapability?.MaxRefreshRateHz);
        Assert.Contains(replugged, settings.KnownPhysicalDisplays.Single().HistoricalIdentities);
    }

    [Fact]
    public void Catalog_reconciliation_keeps_original_profile_record_and_merges_replug_route()
    {
        var original = CreateIdentity("S90F") with
        {
            AdapterLuid = 65578,
            TargetId = 4352,
            MonitorDevicePath = "PATH-S90F",
            EdidProductCodeId = 30903,
            OutputTechnology = "HDMI"
        };
        var degraded = original with
        {
            FriendlyName = "SAMSUNG",
            AdapterLuid = 65813,
            MonitorDevicePath = "PATH-SAMSUNG",
            EdidProductCodeId = 30706
        };
        var replugged = original with { AdapterLuid = 65643 };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay
                {
                    Id = "original",
                    PhysicalIdentity = original,
                    HistoricalIdentities = [degraded],
                    CanonicalFriendlyName = "S90F",
                    BestKnownCapability = Capability(3840, 2160, 144)
                },
                new KnownPhysicalDisplay
                {
                    Id = "duplicate",
                    PhysicalIdentity = replugged,
                    CanonicalFriendlyName = "S90F"
                }
            ],
            GamingProfiles =
            [new DisplayLayoutProfile
            {
                Name = "CONSOLE",
                DisplayTopology = new GamingProfileDisplayTopology
                {
                    Displays = [new ProfileDisplayTarget
                    {
                        KnownPhysicalDisplayId = "original",
                        Identity = original,
                        Enabled = true
                    }]
                }
            }]
        };

        Assert.True(KnownPhysicalDisplayCatalogService.MigrateLegacyProfileCapabilities(settings));

        var survivor = Assert.Single(settings.KnownPhysicalDisplays);
        Assert.Equal("original", survivor.Id);
        Assert.Equal("S90F", survivor.CanonicalFriendlyName);
        Assert.Equal(144, survivor.BestKnownCapability?.MaxRefreshRateHz);
        Assert.Contains(replugged, survivor.HistoricalIdentities);

        var current = KnownPhysicalDisplayCatalogService.Merge(
            settings,
            [CreateDisplay(replugged, 144, isEnabled: false)]);
        Assert.Equal(1, current.LogicalCount);
        Assert.Equal("original", current.Displays.Single().KnownPhysicalDisplayId);
        Assert.Equal("S90F", current.Displays.Single().Name);
    }

    [Fact]
    public void Normalization_promotes_richer_historical_route_when_primary_route_is_incomplete()
    {
        var incomplete = CreateIdentity("S90F") with
        {
            AdapterLuid = 0,
            TargetId = 4352,
            MonitorDevicePath = "PATH-S90F",
            EdidIdsValid = true,
            EdidProductCodeId = 30903
        };
        var route = incomplete with { AdapterLuid = 65643 };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [new KnownPhysicalDisplay
            {
                Id = "tv",
                PhysicalIdentity = incomplete,
                CanonicalFriendlyName = "S90F",
                HistoricalIdentities = [route],
                BestKnownCapability = Capability(3840, 2160, 144)
            }]
        };

        Assert.True(KnownPhysicalDisplayCatalogService.MigrateLegacyProfileCapabilities(settings));
        Assert.Equal(65643, settings.KnownPhysicalDisplays.Single().PhysicalIdentity.AdapterLuid);
        Assert.Contains(incomplete, settings.KnownPhysicalDisplays.Single().HistoricalIdentities);
        Assert.Equal(144, settings.KnownPhysicalDisplays.Single().BestKnownCapability?.MaxRefreshRateHz);
    }

    [Fact]
    public void Different_manufacturer_and_stable_device_identity_are_not_reconciled()
    {
        var left = CreateIdentity("S90F") with
        {
            AdapterLuid = 65578,
            TargetId = 4352,
            MonitorDevicePath = "PATH-A",
            EdidManufacturerId = 11596,
            EdidProductCodeId = 30903,
            OutputTechnology = "HDMI"
        };
        var right = left with
        {
            FriendlyName = "OTHER",
            AdapterLuid = 65813,
            MonitorDevicePath = "PATH-B",
            EdidManufacturerId = 9999,
            EdidProductCodeId = 1
        };
        var settings = new DisplaySettings
        {
            KnownPhysicalDisplays =
            [
                new KnownPhysicalDisplay { Id = "A", PhysicalIdentity = left, HistoricalIdentities = [], CanonicalFriendlyName = "S90F", BestKnownCapability = Capability(3840, 2160, 144), LastVerifiedActiveCapability = Capability(3840, 2160, 144) },
                new KnownPhysicalDisplay { Id = "B", PhysicalIdentity = right, HistoricalIdentities = [], CanonicalFriendlyName = "OTHER" }
            ]
        };

        Assert.False(KnownPhysicalDisplayCatalogService.HasKnownDisplayReconciliationCandidates(settings));
        Assert.False(KnownPhysicalDisplayCatalogService.MigrateLegacyProfileCapabilities(settings));
        Assert.Equal(2, settings.KnownPhysicalDisplays.Count);
    }

    private static DisplayDeviceInfo CreateDisplay(string name, uint refresh, bool isEnabled = true)
    {
        return CreateDisplay(CreateIdentity(name), refresh, isEnabled);
    }

    private static DisplayDeviceInfo CreateDisplay(DisplayIdentity identity, uint refresh, bool isEnabled = true)
    {
        var name = identity.FriendlyName;
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
