namespace GameShell.Core.Models;

public sealed class AppStatus
{
    public ModeState State { get; init; } = ModeState.Unknown;
    public string? CurrentMode { get; init; }
    public string? SessionState { get; init; }
    public bool IsBusy { get; init; }
    public string DisplayMode { get; init; } = "NativeDisplayConfig";
    public string? SelectedConsoleDisplay { get; init; }
    public bool HasGamingProfiles { get; init; }
    public string? ActiveProfileName { get; init; }
    public string? ActiveLauncher { get; init; }
    public string? LastOperation { get; init; }
    public long? LastOperationDurationMs { get; init; }
    public string? LastError { get; init; }
    public string SettingsPath { get; init; } = string.Empty;
}
