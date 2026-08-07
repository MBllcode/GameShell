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
        Assert.Equal("Ctrl+Alt+P", settings.Hotkeys.PcGamingMode);
        Assert.Equal("Ctrl+Alt+W", settings.Hotkeys.WorkMode);
        Assert.Null(settings.Display.ConsoleDisplay);
        Assert.Null(settings.Display.PcGamingLayout);
        Assert.True(settings.Audio.SwitchAudioInConsoleMode);
        Assert.True(settings.Audio.RestoreAudioInWorkMode);
        Assert.Null(settings.Audio.ConsoleAudioDeviceId);
    }

    [Fact]
    public void NewSettings_DisableControllerByDefault()
    {
        var settings = AppSettingsValidator.Validate(new AppSettings());

        Assert.False(settings.Controller.Enabled);
        Assert.Null(settings.Controller.ConsoleMode);
        Assert.Null(settings.Controller.PcGamingMode);
        Assert.Null(settings.Controller.WorkMode);
    }

    [Fact]
    public void NewSettings_EnableAutomaticConsoleAudioByDefault()
    {
        var settings = AppSettingsValidator.Validate(new AppSettings());

        Assert.True(settings.Audio.SwitchAudioInConsoleMode);
        Assert.True(settings.Audio.RestoreAudioInWorkMode);
        Assert.Null(settings.Audio.ConsoleAudioDeviceId);
        Assert.Null(settings.Audio.ConsoleAudioDeviceName);
    }

    [Fact]
    public void Validate_MigratesLegacyPcGamingDefaults()
    {
        var settings = new AppSettings
        {
            PcGaming = new PcGamingSettings
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

        Assert.True(validated.PcGaming.HideDesktopIcons);
        Assert.True(validated.PcGaming.HideTaskbar);
        Assert.False(validated.PcGaming.CloseVisibleWindows);
        Assert.False(validated.PcGaming.MinimizeRemainingWindows);
        Assert.True(validated.PcGaming.MoveCursorToCorner);
        Assert.False(validated.PcGaming.HideCursor);
        Assert.True(validated.PcGaming.StartLauncher);
        Assert.True(validated.PcGaming.BringLauncherToForeground);
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
                PcGamingMode = new ControllerShortcutSettings()
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        Assert.Empty(validated.Controller.PcGamingMode!.Buttons);
        Assert.Equal(700, validated.Controller.PcGamingMode.HoldDurationMs);
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
                PcGamingMode = "Ctrl+Alt+P",
                WorkMode = "Ctrl+Alt+W"
            }
        };

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();

        Assert.Equal("Ctrl+Alt+G", loaded.Hotkeys.ConsoleMode);
        Assert.Equal("Ctrl+Alt+P", loaded.Hotkeys.PcGamingMode);
        Assert.Equal("Ctrl+Alt+W", loaded.Hotkeys.WorkMode);
    }

    [Fact]
    public void Validate_DoesNotCopyGlobalControllerShortcutToProfileConfiguration()
    {
        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                PcGamingProfiles =
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
                PcGamingMode = new ControllerShortcutSettings
                {
                    Buttons = [ControllerButton.Back, ControllerButton.South],
                    HoldDurationMs = 900
                }
            }
        };

        var validated = AppSettingsValidator.Validate(settings);
        var configuration = Assert.Single(validated.Display.PcGamingProfileConfigurations);

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
                PcGamingProfiles =
                [
                    new DisplayLayoutProfile { Name = "Salon", Layout = new DisplayLayoutSnapshot() },
                    new DisplayLayoutProfile { Name = "Biurko", Layout = new DisplayLayoutSnapshot() }
                ],
                PcGamingProfileConfigurations =
                [
                    new PcGamingProfileConfiguration
                    {
                        ProfileName = "Salon",
                        ControllerShortcut = new ControllerShortcutSettings
                        {
                            Buttons = [ControllerButton.Back, ControllerButton.South],
                            HoldDurationMs = 700
                        }
                    },
                    new PcGamingProfileConfiguration
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

        Assert.NotNull(validated.Display.PcGamingProfileConfigurations[0].ControllerShortcut);
        Assert.Null(validated.Display.PcGamingProfileConfigurations[1].ControllerShortcut);
    }

    [Fact]
    public void Validate_ClearsProfileKeyboardHotkeyConflictingWithGlobalWorkMode()
    {
        var settings = new AppSettings
        {
            Hotkeys = new HotkeySettings { WorkMode = "Ctrl+Alt+W" },
            Display = new DisplaySettings
            {
                PcGamingProfiles =
                [
                    new DisplayLayoutProfile { Name = "Salon", Layout = new DisplayLayoutSnapshot() }
                ],
                PcGamingProfileConfigurations =
                [
                    new PcGamingProfileConfiguration
                    {
                        ProfileName = "Salon",
                        Hotkeys = new PcGamingProfileHotkeys { EnterProfile = "Alt+Ctrl+W" }
                    }
                ]
            }
        };

        var validated = AppSettingsValidator.Validate(settings);

        Assert.Empty(validated.Display.PcGamingProfileConfigurations[0].Hotkeys.EnterProfile);
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
                PcGamingProfiles =
                [
                    new DisplayLayoutProfile { Name = "Salon", Layout = new DisplayLayoutSnapshot() }
                ],
                PcGamingProfileConfigurations =
                [
                    new PcGamingProfileConfiguration
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

        Assert.Null(validated.Display.PcGamingProfileConfigurations[0].ControllerShortcut);
    }

    private static string CreateTempSettingsPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GameShell.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }
}
