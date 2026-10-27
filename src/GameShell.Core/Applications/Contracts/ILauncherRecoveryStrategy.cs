using GameShell.Core.Settings;

namespace GameShell.Core.Applications;

public interface ILauncherRecoveryStrategy
{
    Task<LauncherRecoveryResult> RecoverAsync(
        AppSettings settings,
        string? profileName,
        int? gameProcessId = null,
        CancellationToken cancellationToken = default);
}

public sealed record LauncherRecoveryResult
{
    public bool Success { get; init; }
    public bool Restarted { get; init; }
    public string Message { get; init; } = string.Empty;

    public static LauncherRecoveryResult Recovered(string message) => new()
    {
        Success = true,
        Message = message
    };

    public static LauncherRecoveryResult RestartedLauncher(string message) => new()
    {
        Success = true,
        Restarted = true,
        Message = message
    };

    public static LauncherRecoveryResult Failed(string message) => new() { Message = message };
}
