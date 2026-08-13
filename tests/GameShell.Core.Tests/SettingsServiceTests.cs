using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_LoadsLegacySettingsWithControllerExecutablePath()
    {
        var settingsPath = CreateTempSettingsPath();
        await File.WriteAllTextAsync(settingsPath, """
            {
              "display": {
                "strategy": "KeepCurrentLayout",
                "gamingDisplayId": null,
                "workPrimaryDisplayId": null,
                "usePrimaryDisplayOnlyInConsoleMode": true,
                "displayFusion": {
                  "enabled": false,
                  "executablePath": "C:\\Program Files\\DisplayFusion\\DisplayFusionCommand.exe",
                  "pcGamingProfileName": null,
                  "gamingProfileName": "ConsoleGaming",
                  "workProfileName": "Work"
                }
              },
              "controller": {
                "executablePath": "C:\\Tools\\LegacyControllerBridge\\ControllerBridge.exe"
              },
              "hotkeys": {
                "consoleMode": "Ctrl+Alt+G",
                "pcGamingMode": "Ctrl+Alt+P",
                "workMode": "Ctrl+Alt+W"
              },
              "ui": {
                "closeToTray": true,
                "isFirstRunCompleted": true
              }
            }
            """);

        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);

        var settings = await service.LoadAsync();

        Assert.False(settings.Controller.Enabled);
        Assert.Null(settings.Controller.ConsoleMode);
        Assert.Equal("Ctrl+Alt+G", settings.Hotkeys.ConsoleMode);
        Assert.Equal("Ctrl+Alt+P", settings.Hotkeys.GamingMode);
        Assert.Equal("Ctrl+Alt+W", settings.Hotkeys.WorkMode);
        Assert.Null(settings.Display.ConsoleDisplay);
        Assert.Null(settings.Display.GamingLayout);
    }

    [Fact]
    public async Task SaveAsync_WritesCanonicalGamingPropertyNames()
    {
        var settingsPath = CreateTempSettingsPath();
        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);

        await service.SaveAsync(new AppSettings
        {
            Gaming = new GamingSettings { GameProcessNames = ["game.exe"] },
            Display = new DisplaySettings
            {
                GamingLayout = new DisplayLayoutSnapshot(),
                GamingProfiles = [new DisplayLayoutProfile { Name = "SALON" }],
                ActiveGamingProfileName = "SALON"
            },
            Hotkeys = new HotkeySettings { GamingMode = "Ctrl+Alt+P" }
        });

        var json = await File.ReadAllTextAsync(settingsPath);

        Assert.Contains("\"gaming\"", json);
        Assert.Contains("\"gamingProfiles\"", json);
        Assert.Contains("\"gamingMode\"", json);
        Assert.DoesNotContain("\"pcGaming", json, StringComparison.Ordinal);
    }

    [Fact]
    public void NewSettings_DisableControllerByDefault()
    {
        var settings = AppSettingsValidator.Validate(new AppSettings());

        Assert.False(settings.Controller.Enabled);
        Assert.Null(settings.Controller.ConsoleMode);
        Assert.Null(settings.Controller.GamingMode);
        Assert.Null(settings.Controller.WorkMode);
    }

    [Fact]
    public void Validate_MigratesLegacyGamingDefaults()
    {
        var settings = new AppSettings
        {
            Gaming = new GamingSettings
            {
                HideDesktopIcons = false,
                HideTaskbar = false,
                CloseVisibleWindows = true,
                MinimizeRemainingWindows = false,
                MoveCursorToCorner = false,
                HideCursor = true,
                StartLauncher = false,
                BringLauncherToForeground = false
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        Assert.True(validated.Gaming.HideDesktopIcons);
        Assert.True(validated.Gaming.HideTaskbar);
        Assert.False(validated.Gaming.CloseVisibleWindows);
        Assert.False(validated.Gaming.MinimizeRemainingWindows);
        Assert.True(validated.Gaming.MoveCursorToCorner);
        Assert.False(validated.Gaming.HideCursor);
        Assert.True(validated.Gaming.StartLauncher);
        Assert.True(validated.Gaming.BringLauncherToForeground);
    }

    [Fact]
    public void Validate_NormalizesGamingProfileNamesToUppercase()
    {
        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                ActiveGamingProfileName = "salon",
                GamingProfiles =
                [
                    new DisplayLayoutProfile
                    {
                        Name = "salon",
                        Layout = new DisplayLayoutSnapshot()
                    }
                ],
                GamingProfileConfigurations =
                [
                    new GamingProfileConfiguration { ProfileName = "salon" }
                ]
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        var profile = Assert.Single(validated.Display.GamingProfiles);
        var configuration = Assert.Single(validated.Display.GamingProfileConfigurations);
        Assert.Equal("SALON", profile.Name);
        Assert.Equal("SALON", configuration.ProfileName);
        Assert.Equal("SALON", validated.Display.ActiveGamingProfileName);
    }

    [Fact]
    public void Validate_DoesNotCreateDefaultProfileWhenAllProfilesAreRemoved()
    {
        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                ActiveGamingProfileName = "DOMYSLNY",
                GamingLayout = new DisplayLayoutSnapshot(),
                GamingProfiles = []
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        Assert.Empty(validated.Display.GamingProfiles);
        Assert.Null(validated.Display.ActiveGamingProfileName);
        Assert.Null(validated.Display.GamingLayout);
    }

    [Theory]
    [InlineData(100, 300)]
    [InlineData(700, 700)]
    [InlineData(4000, 3000)]
    public void Validate_ClampsControllerHoldDuration(int input, int expected)
    {
        var settings = new AppSettings
        {
            Controller = new ControllerSettings
            {
                ConsoleMode = new ControllerShortcutSettings
                {
                    Buttons = [ControllerButton.Back],
                    HoldDurationMs = input
                }
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        Assert.Equal(expected, validated.Controller.ConsoleMode!.HoldDurationMs);
    }

    [Fact]
    public void Validate_RemovesDuplicateButtonsFromShortcut()
    {
        var settings = new AppSettings
        {
            Controller = new ControllerSettings
            {
                WorkMode = new ControllerShortcutSettings
                {
                    Buttons = [ControllerButton.Back, ControllerButton.Back, ControllerButton.South],
                    HoldDurationMs = 700
                }
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        Assert.Equal([ControllerButton.Back, ControllerButton.South], validated.Controller.WorkMode!.Buttons);
    }

    [Fact]
    public void Validate_AllowsEmptyControllerShortcut()
    {
        var settings = new AppSettings
        {
            Controller = new ControllerSettings
            {
                GamingMode = new ControllerShortcutSettings()
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        Assert.Empty(validated.Controller.GamingMode!.Buttons);
        Assert.Equal(700, validated.Controller.GamingMode.HoldDurationMs);
    }

    [Fact]
    public async Task SaveAsync_PreservesExistingKeyboardHotkeys()
    {
        var settingsPath = CreateTempSettingsPath();
        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        var settings = new AppSettings
        {
            Hotkeys = new HotkeySettings
            {
                ConsoleMode = "Ctrl+Alt+G",
                GamingMode = "Ctrl+Alt+P",
                WorkMode = "Ctrl+Alt+W"
            }
        };

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();

        Assert.Equal("Ctrl+Alt+G", loaded.Hotkeys.ConsoleMode);
        Assert.Equal("Ctrl+Alt+P", loaded.Hotkeys.GamingMode);
        Assert.Equal("Ctrl+Alt+W", loaded.Hotkeys.WorkMode);
    }

    [Fact]
    public async Task SaveAndReload_PreservesExplicitFixedProfileRationalRefresh()
    {
        var settingsPath = CreateTempSettingsPath();
        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                GamingProfiles =
                [
                    new DisplayLayoutProfile
                    {
                        Name = "KONSOLA",
                        Layout = new DisplayLayoutSnapshot(),
                        DisplayTopology = new GamingProfileDisplayTopology
                        {
                            Displays =
                            [
                                new ProfileDisplayTarget
                                {
                                    Enabled = true,
                                    Width = 3840,
                                    Height = 2160,
                                    RefreshPolicy = RefreshPolicy.Fixed,
                                    FixedRefreshRateNumerator = 14398,
                                    FixedRefreshRateDenominator = 100
                                }
                            ]
                        }
                    }
                ]
            }
        };

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();
        var target = Assert.Single(Assert.Single(loaded.Display.GamingProfiles).DisplayTopology!.Displays);

                Assert.Equal(RefreshPolicy.Fixed, target.RefreshPolicy);
                Assert.Equal(14398u, target.FixedRefreshRateNumerator);
                Assert.Equal(100u, target.FixedRefreshRateDenominator);
    }

        [Fact]
        public async Task LoadAsync_ResolvesLegacyExactRefreshToAutoHighest()
        {
                var settingsPath = CreateTempSettingsPath();
                await File.WriteAllTextAsync(settingsPath, """
                        {
                            "display": {
                                "pcGamingProfiles": [{
                                    "name": "KONSOLA",
                                    "layout": { "displays": [] },
                                    "displayTopology": { "displays": [{
                                        "identity": { "friendlyName": "S90F" },
                                        "enabled": true,
                                        "width": 3840,
                                        "height": 2160,
                                        "refreshRateNumerator": 60,
                                        "refreshRateDenominator": 1
                                    }] }
                                }]
                            }
                        }
                        """);

                var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
                var settings = await service.LoadAsync();
                var target = Assert.Single(Assert.Single(settings.Display.GamingProfiles).DisplayTopology!.Displays);

                Assert.Equal(RefreshPolicy.AutoHighest, target.RefreshPolicy);
                Assert.Equal(0u, target.RefreshRateNumerator);
                Assert.Equal(0u, target.RefreshRateDenominator);
                Assert.Equal(0u, target.FixedRefreshRateNumerator);
                Assert.Equal(0u, target.FixedRefreshRateDenominator);

                await service.SaveAsync(settings);
                var reloaded = await service.LoadAsync();
                var savedTarget = Assert.Single(Assert.Single(reloaded.Display.GamingProfiles).DisplayTopology!.Displays);

                Assert.Equal(RefreshPolicy.AutoHighest, savedTarget.RefreshPolicy);
                Assert.Equal(0u, savedTarget.RefreshRateNumerator);
                Assert.Equal(0u, savedTarget.RefreshRateDenominator);
                Assert.Equal(0u, savedTarget.FixedRefreshRateNumerator);
                Assert.Equal(0u, savedTarget.FixedRefreshRateDenominator);
        }

        [Fact]
        public void Validate_RepairsPreviouslyAutoMigratedFixedTarget()
        {
                var target = new ProfileDisplayTarget
                {
                        RefreshPolicy = RefreshPolicy.Fixed,
                        FixedRefreshRateNumerator = 60,
                        FixedRefreshRateDenominator = 1,
                        RefreshRateNumerator = 60,
                        RefreshRateDenominator = 1
                };

                AppSettingsValidator.Validate(new AppSettings
                {
                        Display = new DisplaySettings
                        {
                                GamingProfiles = [new DisplayLayoutProfile { Name = "KONSOLA", DisplayTopology = new GamingProfileDisplayTopology { Displays = [target] } }]
                        }
                });

                Assert.Equal(RefreshPolicy.AutoHighest, target.RefreshPolicy);
                Assert.Equal(0u, target.FixedRefreshRateNumerator);
        }

    [Fact]
    public async Task SaveAndReload_PreservesAutoHighestPolicyWithoutExactRefresh()
    {
        var settingsPath = CreateTempSettingsPath();
        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                GamingProfiles =
                [
                    new DisplayLayoutProfile
                    {
                        Name = "KONSOLA",
                        Layout = new DisplayLayoutSnapshot(),
                        DisplayTopology = new GamingProfileDisplayTopology
                        {
                            Displays =
                            [
                                new ProfileDisplayTarget
                                {
                                    Enabled = true,
                                    Width = 3840,
                                    Height = 2160,
                                    RefreshPolicy = RefreshPolicy.AutoHighest
                                }
                            ]
                        }
                    }
                ]
            }
        };

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();
        var target = Assert.Single(Assert.Single(loaded.Display.GamingProfiles).DisplayTopology!.Displays);

        Assert.Equal(RefreshPolicy.AutoHighest, target.RefreshPolicy);
        Assert.Equal(0u, target.FixedRefreshRateNumerator);
        Assert.Equal(0u, target.FixedRefreshRateDenominator);
    }

    [Fact]
    public void Validate_DoesNotCopyGlobalControllerShortcutToProfileConfiguration()
    {
        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                GamingProfiles =
                [
                    new DisplayLayoutProfile
                    {
                        Name = "Salon",
                        Layout = new DisplayLayoutSnapshot()
                    }
                ]
            },
            Controller = new ControllerSettings
            {
                GamingMode = new ControllerShortcutSettings
                {
                    Buttons = [ControllerButton.Back, ControllerButton.South],
                    HoldDurationMs = 900
                }
            }
        };

        var validated = AppSettingsValidator.Validate(settings);
        var configuration = Assert.Single(validated.Display.GamingProfileConfigurations);

        Assert.Null(configuration.ControllerShortcut);
    }

    [Fact]
    public void Validate_ClearsDuplicateProfileControllerShortcuts()
    {
        var shortcut = new ControllerShortcutSettings
        {
            Buttons = [ControllerButton.Back, ControllerButton.South],
            HoldDurationMs = 700
        };

        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                GamingProfiles =
                [
                    new DisplayLayoutProfile { Name = "Salon", Layout = new DisplayLayoutSnapshot() },
                    new DisplayLayoutProfile { Name = "Biurko", Layout = new DisplayLayoutSnapshot() }
                ],
                GamingProfileConfigurations =
                [
                    new GamingProfileConfiguration
                    {
                        ProfileName = "Salon",
                        ControllerShortcut = new ControllerShortcutSettings
                        {
                            Buttons = [ControllerButton.Back, ControllerButton.South],
                            HoldDurationMs = 700
                        }
                    },
                    new GamingProfileConfiguration
                    {
                        ProfileName = "Biurko",
                        ControllerShortcut = new ControllerShortcutSettings
                        {
                            Buttons = [ControllerButton.South, ControllerButton.Back],
                            HoldDurationMs = 700
                        }
                    }
                ]
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        Assert.NotNull(validated.Display.GamingProfileConfigurations[0].ControllerShortcut);
        Assert.Null(validated.Display.GamingProfileConfigurations[1].ControllerShortcut);
    }

    [Fact]
    public void Validate_ClearsProfileKeyboardHotkeyConflictingWithGlobalWorkMode()
    {
        var settings = new AppSettings
        {
            Hotkeys = new HotkeySettings { WorkMode = "Ctrl+Alt+W" },
            Display = new DisplaySettings
            {
                GamingProfiles =
                [
                    new DisplayLayoutProfile { Name = "Salon", Layout = new DisplayLayoutSnapshot() }
                ],
                GamingProfileConfigurations =
                [
                    new GamingProfileConfiguration
                    {
                        ProfileName = "Salon",
                        Hotkeys = new GamingProfileHotkeys { EnterProfile = "Alt+Ctrl+W" }
                    }
                ]
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        Assert.Empty(validated.Display.GamingProfileConfigurations[0].Hotkeys.EnterProfile);
    }

    [Fact]
    public void Validate_ClearsProfileControllerShortcutConflictingWithGlobalWorkMode()
    {
        var settings = new AppSettings
        {
            Controller = new ControllerSettings
            {
                WorkMode = new ControllerShortcutSettings
                {
                    Buttons = [ControllerButton.Back, ControllerButton.South]
                }
            },
            Display = new DisplaySettings
            {
                GamingProfiles =
                [
                    new DisplayLayoutProfile { Name = "Salon", Layout = new DisplayLayoutSnapshot() }
                ],
                GamingProfileConfigurations =
                [
                    new GamingProfileConfiguration
                    {
                        ProfileName = "Salon",
                        ControllerShortcut = new ControllerShortcutSettings
                        {
                            Buttons = [ControllerButton.South, ControllerButton.Back]
                        }
                    }
                ]
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        Assert.Null(validated.Display.GamingProfileConfigurations[0].ControllerShortcut);
    }

    [Fact]
    public async Task LoadAsync_ReusesSnapshotUntilSettingsFileChanges()
    {
        var settingsPath = CreateTempSettingsPath();
        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        await service.SaveAsync(new AppSettings());

        var first = await service.LoadAsync();
        var second = await service.LoadAsync();
        Assert.Same(first, second);

        await File.WriteAllTextAsync(settingsPath, "{\"ui\":{\"isFirstRunCompleted\":true}}");
        File.SetLastWriteTimeUtc(settingsPath, DateTime.UtcNow.AddSeconds(2));

        var reloaded = await service.LoadAsync();
        Assert.NotSame(first, reloaded);
        Assert.True(reloaded.Ui.IsFirstRunCompleted);
    }

    [Fact]
    public async Task LoadAsync_RecoversFromCorruptedPrimaryUsingBackup()
    {
        var settingsPath = CreateTempSettingsPath();
        await File.WriteAllTextAsync(settingsPath + ".bak", """
            {
              "ui": {
                "closeToTray": true,
                "isFirstRunCompleted": true
              },
              "display": {
                "pcGamingProfiles": [
                  {
                    "name": "ALPHA",
                    "layout": { "displays": [] }
                  }
                ]
              }
            }
            """);

        await File.WriteAllTextAsync(settingsPath, "{\"ui\":{\"closeToTray\":true,\"windowWidth\":1516.8000000000002");

        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        var loaded = await service.LoadAsync();

        Assert.True(loaded.Ui.IsFirstRunCompleted);
        Assert.Equal("ALPHA", Assert.Single(loaded.Display.GamingProfiles).Name);
    }

    [Fact]
    public async Task LoadAsync_RecoversFromMissingPrimaryUsingBackup()
    {
        var settingsPath = CreateTempSettingsPath();
        await File.WriteAllTextAsync(settingsPath + ".bak", """
            {
              "ui": {
                "closeToTray": true,
                "isFirstRunCompleted": true
              },
              "display": {
                "pcGamingProfiles": [
                  {
                    "name": "BETA",
                    "layout": { "displays": [] }
                  }
                ]
              }
            }
            """);

        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        var loaded = await service.LoadAsync();

        Assert.True(loaded.Ui.IsFirstRunCompleted);
        Assert.Equal("BETA", Assert.Single(loaded.Display.GamingProfiles).Name);
    }

    [Fact]
    public async Task SaveAsync_DoesNotCorruptPrimaryUnderConcurrentAccess()
    {
        var settingsPath = CreateTempSettingsPath();
        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        var first = new AppSettings { Ui = new UiSettings { IsFirstRunCompleted = true }, Display = new DisplaySettings { GamingProfiles = [new DisplayLayoutProfile { Name = "FIRST", Layout = new DisplayLayoutSnapshot() }] } };
        var second = new AppSettings { Ui = new UiSettings { IsFirstRunCompleted = true }, Display = new DisplaySettings { GamingProfiles = [new DisplayLayoutProfile { Name = "SECOND", Layout = new DisplayLayoutSnapshot() }] } };

        await Task.WhenAll(service.SaveAsync(first), service.SaveAsync(second));

        var loaded = await service.LoadAsync();
        Assert.True(loaded.Ui.IsFirstRunCompleted);
        Assert.Contains(loaded.Display.GamingProfiles, profile => string.Equals(profile.Name, "SECOND", StringComparison.OrdinalIgnoreCase)
            || string.Equals(profile.Name, "FIRST", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SaveAsync_GetsLatestStateWhenWritesQueue()
    {
        var settingsPath = CreateTempSettingsPath();
        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        var first = new AppSettings { Ui = new UiSettings { CloseToTray = true } };
        var second = new AppSettings { Ui = new UiSettings { CloseToTray = false, IsFirstRunCompleted = true }, Display = new DisplaySettings { GamingProfiles = [new DisplayLayoutProfile { Name = "LATEST", Layout = new DisplayLayoutSnapshot() }] } };

        await service.SaveAsync(first);
        await service.SaveAsync(second);

        var loaded = await service.LoadAsync();
        Assert.False(loaded.Ui.CloseToTray);
        Assert.True(loaded.Ui.IsFirstRunCompleted);
        Assert.Equal("LATEST", Assert.Single(loaded.Display.GamingProfiles).Name);
    }

    [Fact]
    public async Task LoadAsync_IgnoresStaleTempFileWhenPrimaryIsValid()
    {
        var settingsPath = CreateTempSettingsPath();
        var tempPath = settingsPath + ".tmp";
        var primary = new AppSettings { Ui = new UiSettings { IsFirstRunCompleted = true }, Display = new DisplaySettings { GamingProfiles = [new DisplayLayoutProfile { Name = "PRIMARY", Layout = new DisplayLayoutSnapshot() }] } };
        var service = new SettingsService(NullLogger<SettingsService>.Instance, settingsPath);
        await service.SaveAsync(primary);
        await File.WriteAllTextAsync(tempPath, "{\"ui\":{\"isFirstRunCompleted\":false}}"
            );

        var loaded = await service.LoadAsync();

        Assert.Equal("PRIMARY", Assert.Single(loaded.Display.GamingProfiles).Name);
        Assert.True(loaded.Ui.IsFirstRunCompleted);
    }

    private static string CreateTempSettingsPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GameShell.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }
}
