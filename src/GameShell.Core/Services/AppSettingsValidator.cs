using GameShell.Core.Models;
using System.Globalization;

namespace GameShell.Core.Services;

public static class AppSettingsValidator
{
    public const int CurrentSettingsSchemaVersion = 4;

    public static AppSettings Validate(AppSettings settings)
    {
        settings.SettingsSchemaVersion = CurrentSettingsSchemaVersion;
        settings.Display ??= new DisplaySettings();
        settings.Display.GamingProfiles ??= [];
        settings.Display.GamingProfileConfigurations ??= [];
        settings.Display.GamingProfileBehaviours ??= [];
        settings.Display.KnownPhysicalDisplays ??= [];
        settings.Launcher ??= new LauncherSettings();
        settings.GameShell ??= new GameShellSettings();
        settings.Gaming ??= new GamingSettings();
        settings.WorkMode ??= new WorkModeSettings();
        settings.Controller ??= new ControllerSettings();
        settings.Hotkeys ??= new HotkeySettings();
        settings.Startup ??= new StartupSettings();
        settings.Ui ??= new UiSettings();
        settings.RemoteControl ??= new RemoteControlSettings();
        settings.RemoteControl.Port = Math.Clamp(settings.RemoteControl.Port, 1024, 65535);
        settings.RemoteControl.AuthenticationToken ??= string.Empty;
        settings.Ui.Language = NormalizeLanguage(settings.Ui.Language ?? CultureInfo.CurrentUICulture.Name);

        settings.GameShell.CompletionDelaySeconds = Math.Clamp(settings.GameShell.CompletionDelaySeconds, 0, 600);
        settings.GameShell.ProtectedProcessNames ??= [];
        settings.GameShell.CloseOnlyProcessNames ??= [];
        MigrateLegacyGamingDefaults(settings.Gaming);
        settings.Gaming.CompletionDelaySeconds = Math.Clamp(settings.Gaming.CompletionDelaySeconds, 0, 600);
        settings.Gaming.ProtectedProcessNames ??= [];
        settings.Gaming.CloseOnlyProcessNames ??= [];
        settings.WorkMode.TaskbarRestoreDelaySeconds = Math.Clamp(settings.WorkMode.TaskbarRestoreDelaySeconds, 0, 600);
        settings.WorkMode.DesktopIconsDelaySeconds = Math.Clamp(settings.WorkMode.DesktopIconsDelaySeconds, 0, 600);
        settings.WorkMode.CompletionDelaySeconds = Math.Clamp(settings.WorkMode.CompletionDelaySeconds, 0, 600);
        settings.Ui.WindowWidth = ValidateWindowDimension(settings.Ui.WindowWidth, 1040, 10000);
        settings.Ui.WindowHeight = ValidateWindowDimension(settings.Ui.WindowHeight, 720, 10000);
        settings.Ui.WindowLeft = ValidateWindowCoordinate(settings.Ui.WindowLeft);
        settings.Ui.WindowTop = ValidateWindowCoordinate(settings.Ui.WindowTop);
        ValidateControllerShortcut(settings.Controller.ConsoleMode);
        ValidateControllerShortcut(settings.Controller.GamingMode);
        ValidateControllerShortcut(settings.Controller.WorkMode);

        NormalizeGamingProfiles(settings.Display);
        KnownPhysicalDisplayCatalogService.MigrateLegacyProfileCapabilities(settings.Display);
        KnownPhysicalDisplayCatalogService.NormalizeCanonicalNames(settings.Display);
        NormalizeGamingProfileConfigurations(settings.Display, settings.Gaming, settings.Launcher, settings.Hotkeys);
        new ShortcutConflictNormalizer().Normalize(settings);

        return settings;
    }

    public static bool HasLegacyCapabilityData(AppSettings settings)
    {
        return settings.Display?.KnownPhysicalDisplays?.Any(known => known.LastKnownGoodCapability is not null) == true
            || settings.Display?.KnownPhysicalDisplays?.Any(known => string.IsNullOrWhiteSpace(known.Id)) == true
            || settings.Display is not null
                && KnownPhysicalDisplayCatalogService.HasKnownDisplayReconciliationCandidates(settings.Display);
    }

    private static string NormalizeLanguage(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = CultureInfo.CurrentUICulture.Name;
        }

        if (normalized.StartsWith("pl", StringComparison.OrdinalIgnoreCase))
        {
            return "pl";
        }

        return normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "en";
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

    private static void MigrateLegacyGamingDefaults(GamingSettings settings)
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

    private static void NormalizeGamingProfiles(DisplaySettings display)
    {
        var normalizedProfiles = new List<DisplayLayoutProfile>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in display.GamingProfiles.Where(profile => profile.Layout is not null))
        {
            NormalizeRefreshPolicies(candidate.DisplayTopology);
            var baseName = string.IsNullOrWhiteSpace(candidate.Name) ? "PROFIL" : candidate.Name.Trim().ToUpperInvariant();
            var uniqueName = baseName;
            var suffix = 2;

            while (!usedNames.Add(uniqueName))
            {
                uniqueName = $"{baseName} {suffix++}";
            }

            normalizedProfiles.Add(new DisplayLayoutProfile
            {
                Name = uniqueName,
                Layout = candidate.Layout,
                DisplayTopology = candidate.DisplayTopology
            });
        }

        display.GamingProfiles = normalizedProfiles;

        if (display.GamingProfiles.Count == 0)
        {
            display.ActiveGamingProfileName = null;
            display.GamingLayout = null;
            return;
        }

        var activeProfile = !string.IsNullOrWhiteSpace(display.ActiveGamingProfileName)
            ? display.GamingProfiles.FirstOrDefault(profile => string.Equals(profile.Name, display.ActiveGamingProfileName, StringComparison.OrdinalIgnoreCase))
            : null;

        activeProfile ??= display.GamingProfiles[0];
        display.ActiveGamingProfileName = activeProfile.Name;
        display.GamingLayout = activeProfile.Layout;
    }

    private static void NormalizeRefreshPolicies(GamingProfileDisplayTopology? topology)
    {
        if (topology is null)
        {
            return;
        }

        foreach (var target in topology.Displays)
        {
            if (!target.RefreshPolicy.HasValue)
            {
                target.RefreshPolicy = RefreshPolicy.AutoHighest;
            }

            if (target.RefreshPolicy == RefreshPolicy.Fixed
                && HasValidRefresh(target.FixedRefreshRateNumerator, target.FixedRefreshRateDenominator)
                && target.FixedRefreshRateNumerator == target.RefreshRateNumerator
                && target.FixedRefreshRateDenominator == target.RefreshRateDenominator)
            {
                target.RefreshPolicy = RefreshPolicy.AutoHighest;
            }

            if (target.RefreshPolicy == RefreshPolicy.AutoHighest)
            {
                target.FixedRefreshRateNumerator = 0;
                target.FixedRefreshRateDenominator = 0;
                target.RefreshRateNumerator = 0;
                target.RefreshRateDenominator = 0;
            }
        }
    }

    private static bool HasValidRefresh(uint numerator, uint denominator)
    {
        return numerator > 0 && denominator > 0;
    }

    private static void NormalizeGamingProfileConfigurations(
        DisplaySettings display,
        GamingSettings defaultBehaviour,
        LauncherSettings defaultLauncher,
        HotkeySettings defaultHotkeys)
    {
        if (display.GamingProfiles.Count == 0)
        {
            display.GamingProfileConfigurations = [];
            display.GamingProfileBehaviours = [];
            return;
        }

        var normalized = new List<GamingProfileConfiguration>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in display.GamingProfiles)
        {
            var existingConfiguration = ResolveProfileConfiguration(display.GamingProfileConfigurations, profile.Name);
            var legacyBehaviour = ResolveProfileBehaviour(display.GamingProfileBehaviours, profile.Name);

            var behaviour = existingConfiguration?.Behaviour
                ?? legacyBehaviour
                ?? CloneGamingSettings(defaultBehaviour);
            var launcher = existingConfiguration?.Launcher ?? CloneLauncherSettings(defaultLauncher);
            var hotkeys = existingConfiguration?.Hotkeys ?? new GamingProfileHotkeys
            {
                EnterProfile = defaultHotkeys.GamingMode,
                EnterWorkMode = defaultHotkeys.WorkMode
            };
            var controllerShortcut = existingConfiguration?.ControllerShortcut;

            MigrateLegacyGamingDefaults(behaviour);
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

            normalized.Add(new GamingProfileConfiguration
            {
                ProfileName = profile.Name,
                Behaviour = behaviour,
                Launcher = launcher,
                Hotkeys = hotkeys,
                ControllerShortcut = controllerShortcut
            });
        }

        display.GamingProfileConfigurations = normalized;
        display.GamingProfileBehaviours = normalized
            .Select(configuration => new GamingProfileBehaviour
            {
                ProfileName = configuration.ProfileName,
                Behaviour = configuration.Behaviour
            })
            .ToList();
    }

    private static GamingProfileConfiguration? ResolveProfileConfiguration(IEnumerable<GamingProfileConfiguration> candidates, string profileName)
    {
        return candidates.FirstOrDefault(candidate => string.Equals(candidate.ProfileName, profileName, StringComparison.OrdinalIgnoreCase));
    }

    private static GamingSettings? ResolveProfileBehaviour(IEnumerable<GamingProfileBehaviour> candidates, string profileName)
    {
        var matched = candidates.FirstOrDefault(candidate => string.Equals(candidate.ProfileName, profileName, StringComparison.OrdinalIgnoreCase));
        return matched?.Behaviour;
    }

    private static GamingSettings CloneGamingSettings(GamingSettings source)
    {
        return new GamingSettings
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
            GameProcessNames = source.GameProcessNames is null ? [] : [..source.GameProcessNames],
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
