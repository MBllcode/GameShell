namespace GameShell.Core.Models;

public sealed class AppSettings
{
    public int? SettingsSchemaVersion { get; set; }
    public DisplaySettings Display { get; set; } = new();
    public LauncherSettings Launcher { get; set; } = new();
    public GameShellSettings GameShell { get; set; } = new();
    public GamingSettings Gaming { get; set; } = new();
    public WorkModeSettings WorkMode { get; set; } = new();
    public ControllerSettings Controller { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public StartupSettings Startup { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
    public RemoteControlSettings RemoteControl { get; set; } = new();
}

public sealed class RemoteControlSettings
{
    public bool Enabled { get; set; }
    public int Port { get; set; } = 47777;
    public string AuthenticationToken { get; set; } = string.Empty;
    public bool AllowPrivateNetworkOnly { get; set; } = true;
}

public sealed class DisplaySettings
{
    public DisplayIdentity? ConsoleDisplay { get; set; }
    public List<DisplayIdentity> ConsoleDisplays { get; set; } = [];
    public DisplayLayoutSnapshot? GamingLayout { get; set; }
    public List<DisplayLayoutProfile> GamingProfiles { get; set; } = [];
    public string? ActiveGamingProfileName { get; set; }
    public List<GamingProfileConfiguration> GamingProfileConfigurations { get; set; } = [];
    public List<GamingProfileBehaviour> GamingProfileBehaviours { get; set; } = [];
    public List<KnownPhysicalDisplay> KnownPhysicalDisplays { get; set; } = [];
}

public sealed class DisplayLayoutProfile
{
    /// <summary>
    /// Stable profile identity used by startup/runtime references. The nullable
    /// shape keeps older settings files loadable; validation assigns an ID.
    /// </summary>
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DisplayLayoutSnapshot Layout { get; set; } = new();
    public GamingProfileDisplayTopology? DisplayTopology { get; set; }
}

public sealed class GamingProfileDisplayTopology
{
    public List<ProfileDisplayTarget> Displays { get; set; } = [];
}

public enum RefreshPolicy
{
    AutoHighest,
    Fixed
}

public sealed class ProfileDisplayTarget
{
    // Stable catalog reference. Identity remains as a compatibility/runtime
    // mirror for older settings and existing display APIs.
    public string KnownPhysicalDisplayId { get; set; } = string.Empty;
    public DisplayIdentity Identity { get; set; } = new();
    public DisplayCapabilityExpectation? CapabilityExpectation { get; set; }
    public bool Enabled { get; set; }
    public bool IsPrimary { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public RefreshPolicy? RefreshPolicy { get; set; }
    public uint FixedRefreshRateNumerator { get; set; }
    public uint FixedRefreshRateDenominator { get; set; }

    // Retained so older settings files can be read without data loss during migration.
    public uint RefreshRateNumerator { get; set; }
    public uint RefreshRateDenominator { get; set; }
    public DisplayOrientation Orientation { get; set; } = DisplayOrientation.Identity;

    public RefreshPolicy EffectiveRefreshPolicy => RefreshPolicy
        ?? (RefreshRateNumerator > 0 && RefreshRateDenominator > 0 ? Models.RefreshPolicy.Fixed : Models.RefreshPolicy.AutoHighest);

    public uint EffectiveFixedRefreshRateNumerator => FixedRefreshRateNumerator > 0 ? FixedRefreshRateNumerator : RefreshRateNumerator;

    public uint EffectiveFixedRefreshRateDenominator => FixedRefreshRateDenominator > 0 ? FixedRefreshRateDenominator : RefreshRateDenominator;
}

public sealed class DisplayCapabilityExpectation
{
    public int Width { get; set; }
    public int Height { get; set; }
    public uint LastKnownGoodMaxRefreshNumerator { get; set; }
    public uint LastKnownGoodMaxRefreshDenominator { get; set; }

    public double MaxRefreshRateHz => LastKnownGoodMaxRefreshDenominator == 0
        ? 0
        : (double)LastKnownGoodMaxRefreshNumerator / LastKnownGoodMaxRefreshDenominator;
}

public sealed class GamingProfileBehaviour
{
    public string ProfileName { get; set; } = string.Empty;
    public GamingSettings Behaviour { get; set; } = new();
}

public sealed class GamingProfileConfiguration
{
    public string ProfileName { get; set; } = string.Empty;
    public GamingSettings Behaviour { get; set; } = new();
    public LauncherSettings Launcher { get; set; } = new();
    public GamingProfileHotkeys Hotkeys { get; set; } = new();
    public ControllerShortcutSettings? ControllerShortcut { get; set; }
}

public sealed class GamingProfileHotkeys
{
    public string EnterProfile { get; set; } = "";
    public string EnterWorkMode { get; set; } = "";
}

public sealed class LauncherSettings
{
    public string Type { get; set; } = "Playnite";
    public string ExecutablePath { get; set; } = @"%LOCALAPPDATA%\Playnite\Playnite.FullscreenApp.exe";
    public string Arguments { get; set; } = "--startfullscreen --hidesplashscreen";
    public string? TargetDisplayId { get; set; }
}

public class GameShellSettings
{
    public bool HideDesktopIcons { get; set; } = true;
    public bool HideTaskbar { get; set; } = true;
    public bool CloseVisibleWindows { get; set; } = true;
    public bool MinimizeRemainingWindows { get; set; } = true;
    public bool MoveCursorToCorner { get; set; } = true;
    public bool HideCursor { get; set; } = true;
    public bool StartLauncher { get; set; } = true;
    public bool BringLauncherToForeground { get; set; } = true;
    public List<string> ProtectedProcessNames { get; set; } =
    [
        "GameShell",
        "GameShell.Desktop",
        "explorer",
        "powershell",
        "pwsh",
        "Playnite.FullscreenApp",
        "Playnite.DesktopApp",
        "steam",
        "steamwebhelper"
    ];
    public List<string> CloseOnlyProcessNames { get; set; } = [];
    public int CompletionDelaySeconds { get; set; } = 5;
}

public sealed class GamingSettings : GameShellSettings
{
    public List<string> GameProcessNames { get; set; } = [];

    public GamingSettings()
    {
        HideDesktopIcons = true;
        HideTaskbar = true;
        CloseVisibleWindows = false;
        MinimizeRemainingWindows = false;
        MoveCursorToCorner = true;
        HideCursor = false;
        StartLauncher = true;
        BringLauncherToForeground = true;
    }
}

public sealed class WorkModeSettings
{
    /// <summary>
    /// The user's persistent default desktop display layout. When absent, Work Mode
    /// falls back to the runtime layout captured before entering a gaming profile.
    /// </summary>
    public DisplayLayoutSnapshot? DefaultDisplayLayout { get; set; }
    public bool CloseLauncher { get; set; } = true;
    public bool ShowTaskbars { get; set; } = true;
    public bool ShowDesktopIcons { get; set; } = true;
    public int TaskbarRestoreDelaySeconds { get; set; } = 3;
    public int DesktopIconsDelaySeconds { get; set; } = 5;
    public bool DoNotMinimizeWindows { get; set; } = true;
    public int CompletionDelaySeconds { get; set; }
}

public sealed class HotkeySettings
{
    public string ConsoleMode { get; set; } = "";
    public string GamingMode { get; set; } = "";
    public string WorkMode { get; set; } = "";
}

public sealed class StartupSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimizedToTray { get; set; }
    public bool CreateStartMenuShortcut { get; set; } = true;
    public bool CreateDesktopShortcut { get; set; }
    public StartupBehavior StartupBehavior { get; set; } = StartupBehavior.AlwaysWorkMode;
    public string? StartupProfileId { get; set; }
}

/// <summary>
/// Selects the target GameShell converges to after sign-in. AlwaysWorkMode is
/// intentionally zero/default to preserve the historical Work Mode startup behavior.
/// </summary>
public enum StartupBehavior
{
    AlwaysWorkMode = 0,
    RestoreLastActiveMode = 1,
    AlwaysProfile = 2
}

public sealed class UiSettings
{
    public bool CloseToTray { get; set; } = true;
    public bool IsFirstRunCompleted { get; set; }
    public string Language { get; set; } = "en";
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public double? WindowDpiScaleX { get; set; }
    public double? WindowDpiScaleY { get; set; }
}
