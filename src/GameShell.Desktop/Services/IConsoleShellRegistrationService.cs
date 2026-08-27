namespace GameShell.Desktop.Services;

public interface IConsoleShellRegistrationService
{
    Task<ConsoleShellSupportStatus> GetSupportStatusAsync(CancellationToken cancellationToken = default);
    Task<ConsoleShellRegistrationResult> ConfigureAsync(bool enabled, CancellationToken cancellationToken = default);
}

public sealed record ConsoleShellSupportStatus(bool IsSupported, string Edition, string Reason);

public sealed record ConsoleShellRegistrationResult(bool Success, bool Enabled, string Message);
