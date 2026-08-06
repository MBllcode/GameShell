namespace GameShell.Core.Models;

public sealed class AppSettings
{
    public DisplaySettings Display { get; set; } = new();
    public LauncherSettings Launcher { get; set; } = new();
    public GameShellSettings GameShell { get; set; } = new();
    public PcGamingSettings PcGaming { get; set; } = new();
    public WorkModeSettings WorkMode { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public ControllerSettings Controller { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public StartupSettings Startup { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
}

public sealed class DisplaySettings
{
    public DisplayIdentity? ConsoleDisplay { get; set; }
    public List<DisplayIdentity> ConsoleDisplays { get; set; } = [];
    public DisplayLayoutSnapshot? PcGamingLayout { get; set; }
    public List<DisplayLayoutProfile> PcGamingProfiles { get; set; } = [];
    public string? ActivePcGamingProfileName { get; set; }
    public List<PcGamingProfileConfiguration> PcGamingProfileConfigurations { get; set; } = [];
    public List<PcGamingProfileBehaviour> PcGamingProfileBehaviours { get; set; } = [];
}

public sealed class DisplayLayoutProfile
{
    public string Name { get; set; } = string.Empty;
    public DisplayLayoutSnapshot Layout { get; set; } = new();
}

public sealed class PcGamingProfileBehaviour
{
    public string ProfileName { get; set; } = string.Empty;
    public PcGamingSettings Behaviour { get; set; } = new();
}

public sealed class PcGamingProfileConfiguration
{
    public string ProfileName { get; set; } = string.Empty;
    public PcGamingSettings Behaviour { get; set; } = new();
    public LauncherSettings Launcher { get; set; } = new();
    public PcGamingProfileHotkeys Hotkeys { get; set; } = new();
}

public sealed class PcGamingProfileHotkeys
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

public sealed class PcGamingSettings : GameShellSettings
{
    public PcGamingSettings()
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
    public bool CloseLauncher { get; set; } = true;
    public bool ShowTaskbars { get; set; } = true;
    public bool ShowDesktopIcons { get; set; } = true;
    public int TaskbarRestoreDelaySeconds { get; set; } = 3;
    public int DesktopIconsDelaySeconds { get; set; } = 5;
    public bool DoNotMinimizeWindows { get; set; } = true;
    public int CompletionDelaySeconds { get; set; }
}

public sealed class AudioSettings
{
    public bool SwitchAudioInConsoleMode { get; set; } = true;
    public bool RestoreAudioInWorkMode { get; set; } = true;
    public string? ConsoleAudioDeviceId { get; set; }
    public string? ConsoleAudioDeviceName { get; set; }
}

public sealed class HotkeySettings
{
    public string ConsoleMode { get; set; } = "";
    public string PcGamingMode { get; set; } = "";
    public string WorkMode { get; set; } = "";
}

public sealed class StartupSettings
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimizedToTray { get; set; }
    public bool CreateStartMenuShortcut { get; set; } = true;
    public bool CreateDesktopShortcut { get; set; }
}

public sealed class UiSettings
{
    public bool CloseToTray { get; set; } = true;
    public bool IsFirstRunCompleted { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
}
