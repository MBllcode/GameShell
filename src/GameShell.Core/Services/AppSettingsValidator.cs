using GameShell.Core.Models;
using System.Globalization;
using System.Text;

namespace GameShell.Core.Services;

public static class AppSettingsValidator
{
    public static AppSettings Validate(AppSettings settings)
    {
        settings.Display ??= new DisplaySettings();
        settings.Display.PcGamingProfiles ??= [];
        settings.Display.PcGamingProfileConfigurations ??= [];
        settings.Display.PcGamingProfileBehaviours ??= [];
        settings.Launcher ??= new LauncherSettings();
        settings.GameShell ??= new GameShellSettings();
        settings.PcGaming ??= new PcGamingSettings();
        settings.WorkMode ??= new WorkModeSettings();
        settings.Audio ??= new AudioSettings();
        settings.Controller ??= new ControllerSettings();
        settings.Hotkeys ??= new HotkeySettings();
        settings.Startup ??= new StartupSettings();
        settings.Ui ??= new UiSettings();

        settings.GameShell.CompletionDelaySeconds = Math.Clamp(settings.GameShell.CompletionDelaySeconds, 0, 600);
        settings.GameShell.ProtectedProcessNames ??= [];
        settings.GameShell.CloseOnlyProcessNames ??= [];
        MigrateLegacyPcGamingDefaults(settings.PcGaming);
        settings.PcGaming.CompletionDelaySeconds = Math.Clamp(settings.PcGaming.CompletionDelaySeconds, 0, 600);
        settings.PcGaming.ProtectedProcessNames ??= [];
        settings.PcGaming.CloseOnlyProcessNames ??= [];
        settings.WorkMode.TaskbarRestoreDelaySeconds = Math.Clamp(settings.WorkMode.TaskbarRestoreDelaySeconds, 0, 600);
        settings.WorkMode.DesktopIconsDelaySeconds = Math.Clamp(settings.WorkMode.DesktopIconsDelaySeconds, 0, 600);
        settings.WorkMode.CompletionDelaySeconds = Math.Clamp(settings.WorkMode.CompletionDelaySeconds, 0, 600);
        settings.Ui.WindowWidth = ValidateWindowDimension(settings.Ui.WindowWidth, 1040, 10000);
        settings.Ui.WindowHeight = ValidateWindowDimension(settings.Ui.WindowHeight, 720, 10000);
        settings.Ui.WindowLeft = ValidateWindowCoordinate(settings.Ui.WindowLeft);
        settings.Ui.WindowTop = ValidateWindowCoordinate(settings.Ui.WindowTop);
        if (string.IsNullOrWhiteSpace(settings.Audio.ConsoleAudioDeviceId))
        {
            settings.Audio.ConsoleAudioDeviceId = null;
            settings.Audio.ConsoleAudioDeviceName = null;
        }

        ValidateControllerShortcut(settings.Controller.ConsoleMode);
        ValidateControllerShortcut(settings.Controller.PcGamingMode);
        ValidateControllerShortcut(settings.Controller.WorkMode);

        NormalizePcGamingProfiles(settings.Display);
        NormalizePcGamingProfileConfigurations(settings.Display, settings.PcGaming, settings.Launcher, settings.Hotkeys);
        EnsureGlobalWorkModeShortcutUniqueness(settings);

        return settings;
    }

    private static double? ValidateWindowDimension(double? value, double minimum, double maximum)
    {
        return value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)
            ? null
            : Math.Clamp(value.Value, minimum, maximum);
    }

    private static double? ValidateWindowCoordinate(double? value)
    {
        return value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)
            ? null
            : Math.Clamp(value.Value, -100000, 100000);
    }

    private static void ValidateControllerShortcut(ControllerShortcutSettings? shortcut)
    {
        if (shortcut is null)
        {
            return;
        }

        shortcut.HoldDurationMs = Math.Clamp(shortcut.HoldDurationMs, 300, 3000);
        if (shortcut.Buttons.Count <= 1)
        {
            return;
        }

        shortcut.Buttons = shortcut.Buttons.Distinct().ToList();
    }

    private static void MigrateLegacyPcGamingDefaults(PcGamingSettings settings)
    {
        if (settings.HideDesktopIcons
            || settings.HideTaskbar
            || !settings.CloseVisibleWindows
            || settings.MinimizeRemainingWindows
            || settings.MoveCursorToCorner
            || !settings.HideCursor
            || settings.StartLauncher
            || settings.BringLauncherToForeground)
        {
            return;
        }

        settings.HideDesktopIcons = true;
        settings.HideTaskbar = true;
        settings.CloseVisibleWindows = false;
        settings.MoveCursorToCorner = true;
        settings.HideCursor = false;
        settings.StartLauncher = true;
        settings.BringLauncherToForeground = true;
    }

    private static void NormalizePcGamingProfiles(DisplaySettings display)
    {
        var normalizedProfiles = new List<DisplayLayoutProfile>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in display.PcGamingProfiles.Where(profile => profile.Layout is not null))
        {
            var baseName = string.IsNullOrWhiteSpace(candidate.Name) ? "Profil" : candidate.Name.Trim();
            var uniqueName = baseName;
            var suffix = 2;

            while (!usedNames.Add(uniqueName))
            {
                uniqueName = $"{baseName} {suffix++}";
            }

            normalizedProfiles.Add(new DisplayLayoutProfile
            {
                Name = uniqueName,
                Layout = candidate.Layout
            });
        }

        if (normalizedProfiles.Count == 0 && display.PcGamingLayout is not null)
        {
            normalizedProfiles.Add(new DisplayLayoutProfile
            {
                Name = "Domyslny",
                Layout = display.PcGamingLayout
            });
        }

        display.PcGamingProfiles = normalizedProfiles;

        if (display.PcGamingProfiles.Count == 0)
        {
            display.ActivePcGamingProfileName = null;
            display.PcGamingLayout = null;
            return;
        }

        var activeProfile = !string.IsNullOrWhiteSpace(display.ActivePcGamingProfileName)
            ? display.PcGamingProfiles.FirstOrDefault(profile => string.Equals(profile.Name, display.ActivePcGamingProfileName, StringComparison.OrdinalIgnoreCase))
            : null;

        activeProfile ??= display.PcGamingProfiles[0];
        display.ActivePcGamingProfileName = activeProfile.Name;
        display.PcGamingLayout = activeProfile.Layout;
    }

    private static void NormalizePcGamingProfileConfigurations(
        DisplaySettings display,
        PcGamingSettings defaultBehaviour,
        LauncherSettings defaultLauncher,
        HotkeySettings defaultHotkeys)
    {
        if (display.PcGamingProfiles.Count == 0)
        {
            display.PcGamingProfileConfigurations = [];
            display.PcGamingProfileBehaviours = [];
            return;
        }

        var normalized = new List<PcGamingProfileConfiguration>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in display.PcGamingProfiles)
        {
            var existingConfiguration = ResolveProfileConfiguration(display.PcGamingProfileConfigurations, profile.Name);
            var legacyBehaviour = ResolveProfileBehaviour(display.PcGamingProfileBehaviours, profile.Name);

            var behaviour = existingConfiguration?.Behaviour
                ?? legacyBehaviour
                ?? ClonePcGamingSettings(defaultBehaviour);
            var launcher = existingConfiguration?.Launcher ?? CloneLauncherSettings(defaultLauncher);
            var hotkeys = existingConfiguration?.Hotkeys ?? new PcGamingProfileHotkeys
            {
                EnterProfile = defaultHotkeys.PcGamingMode,
                EnterWorkMode = defaultHotkeys.WorkMode
            };
            var controllerShortcut = existingConfiguration?.ControllerShortcut;

            MigrateLegacyPcGamingDefaults(behaviour);
            behaviour.CompletionDelaySeconds = Math.Clamp(behaviour.CompletionDelaySeconds, 0, 600);
            behaviour.ProtectedProcessNames ??= [];
            behaviour.CloseOnlyProcessNames ??= [];
            launcher.Type = string.IsNullOrWhiteSpace(launcher.Type) ? defaultLauncher.Type : launcher.Type.Trim();
            launcher.ExecutablePath ??= defaultLauncher.ExecutablePath;
            launcher.Arguments ??= defaultLauncher.Arguments;
            hotkeys.EnterProfile ??= "";
            hotkeys.EnterWorkMode ??= "";
            ValidateControllerShortcut(controllerShortcut);

            if (!usedNames.Add(profile.Name))
            {
                continue;
            }

            normalized.Add(new PcGamingProfileConfiguration
            {
                ProfileName = profile.Name,
                Behaviour = behaviour,
                Launcher = launcher,
                Hotkeys = hotkeys,
                ControllerShortcut = controllerShortcut
            });
        }

        display.PcGamingProfileConfigurations = normalized;
        EnsureUniqueProfileEnterHotkeys(display.PcGamingProfileConfigurations);
        EnsureUniqueProfileControllerShortcuts(display.PcGamingProfileConfigurations);
        display.PcGamingProfileBehaviours = normalized
            .Select(configuration => new PcGamingProfileBehaviour
            {
                ProfileName = configuration.ProfileName,
                Behaviour = configuration.Behaviour
            })
            .ToList();
    }

    private static void EnsureUniqueProfileEnterHotkeys(IReadOnlyList<PcGamingProfileConfiguration> configurations)
    {
        var usedHotkeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuration in configurations)
        {
            var normalized = NormalizeHotkey(configuration.Hotkeys.EnterProfile);
            if (string.IsNullOrEmpty(normalized))
            {
                continue;
            }

            if (!usedHotkeys.Add(normalized))
            {
                configuration.Hotkeys.EnterProfile = "";
            }
        }
    }

    private static void EnsureUniqueProfileControllerShortcuts(IReadOnlyList<PcGamingProfileConfiguration> configurations)
    {
        var usedShortcuts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuration in configurations)
        {
            if (configuration.ControllerShortcut is null || configuration.ControllerShortcut.Buttons.Count == 0)
            {
                continue;
            }

            var normalized = string.Join('+', configuration.ControllerShortcut.Buttons
                .Select(button => button.ToString())
                .OrderBy(button => button, StringComparer.OrdinalIgnoreCase));

            if (!usedShortcuts.Add(normalized))
            {
                configuration.ControllerShortcut = null;
            }
        }
    }

    private static void EnsureGlobalWorkModeShortcutUniqueness(AppSettings settings)
    {
        var workModeHotkey = NormalizeHotkey(settings.Hotkeys.WorkMode);
        if (!string.IsNullOrEmpty(workModeHotkey))
        {
            foreach (var configuration in settings.Display.PcGamingProfileConfigurations)
            {
                if (NormalizeHotkey(configuration.Hotkeys.EnterProfile) == workModeHotkey)
                {
                    configuration.Hotkeys.EnterProfile = "";
                }
            }
        }

        var workModeControllerShortcut = NormalizeControllerShortcut(settings.Controller.WorkMode);
        if (string.IsNullOrEmpty(workModeControllerShortcut))
        {
            return;
        }

        foreach (var configuration in settings.Display.PcGamingProfileConfigurations)
        {
            if (NormalizeControllerShortcut(configuration.ControllerShortcut) == workModeControllerShortcut)
            {
                configuration.ControllerShortcut = null;
            }
        }
    }

    private static string NormalizeControllerShortcut(ControllerShortcutSettings? shortcut)
    {
        if (shortcut is null || shortcut.Buttons.Count == 0)
        {
            return string.Empty;
        }

        return string.Join('+', shortcut.Buttons
            .Select(button => button.ToString())
            .OrderBy(button => button, StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizeHotkey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        var keyPart = RemoveDiacritics(parts[^1]).ToUpperInvariant();
        var modifierParts = parts
            .Take(parts.Count - 1)
            .Select(part => RemoveDiacritics(part).ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var normalizedModifiers = new List<string>();
        if (modifierParts.Contains("ctrl") || modifierParts.Contains("control"))
        {
            normalizedModifiers.Add("Ctrl");
        }

        if (modifierParts.Contains("alt"))
        {
            normalizedModifiers.Add("Alt");
        }

        if (modifierParts.Contains("shift"))
        {
            normalizedModifiers.Add("Shift");
        }

        if (modifierParts.Contains("win") || modifierParts.Contains("windows") || modifierParts.Contains("meta"))
        {
            normalizedModifiers.Add("Win");
        }

        if (normalizedModifiers.Count == 0 || string.IsNullOrWhiteSpace(keyPart))
        {
            return string.Empty;
        }

        normalizedModifiers.Add(keyPart);
        return string.Join('+', normalizedModifiers);
    }

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var filtered = new char[normalized.Length];
        var index = 0;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            filtered[index++] = character;
        }

        return new string(filtered, 0, index).Normalize(NormalizationForm.FormC);
    }

    private static PcGamingProfileConfiguration? ResolveProfileConfiguration(IEnumerable<PcGamingProfileConfiguration> candidates, string profileName)
    {
        return candidates.FirstOrDefault(candidate => string.Equals(candidate.ProfileName, profileName, StringComparison.OrdinalIgnoreCase));
    }

    private static PcGamingSettings? ResolveProfileBehaviour(IEnumerable<PcGamingProfileBehaviour> candidates, string profileName)
    {
        var matched = candidates.FirstOrDefault(candidate => string.Equals(candidate.ProfileName, profileName, StringComparison.OrdinalIgnoreCase));
        return matched?.Behaviour;
    }

    private static PcGamingSettings ClonePcGamingSettings(PcGamingSettings source)
    {
        return new PcGamingSettings
        {
            HideDesktopIcons = source.HideDesktopIcons,
            HideTaskbar = source.HideTaskbar,
            CloseVisibleWindows = source.CloseVisibleWindows,
            MinimizeRemainingWindows = source.MinimizeRemainingWindows,
            MoveCursorToCorner = source.MoveCursorToCorner,
            HideCursor = source.HideCursor,
            StartLauncher = source.StartLauncher,
            BringLauncherToForeground = source.BringLauncherToForeground,
            ProtectedProcessNames = source.ProtectedProcessNames is null ? [] : [..source.ProtectedProcessNames],
            CloseOnlyProcessNames = source.CloseOnlyProcessNames is null ? [] : [..source.CloseOnlyProcessNames],
            CompletionDelaySeconds = source.CompletionDelaySeconds
        };
    }

    private static LauncherSettings CloneLauncherSettings(LauncherSettings source)
    {
        return new LauncherSettings
        {
            Type = source.Type,
            ExecutablePath = source.ExecutablePath,
            Arguments = source.Arguments,
            TargetDisplayId = source.TargetDisplayId
        };
    }

}
