using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GameShell.Desktop.Services;

/// <summary>
/// Applies Microsoft's per-user "Custom User Interface" WinLogon policy.
/// The policy is supported on Pro, Enterprise, Education and IoT Enterprise
/// and starts the configured interface instead of Explorer at the next sign-in.
/// </summary>
public sealed class WindowsConsoleShellRegistrationService : IConsoleShellRegistrationService
{
    private const string CustomShellPolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string CustomShellValueName = "Shell";

    private static readonly HashSet<string> SupportedEditions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Professional", "ProfessionalN", "ProfessionalEducation", "ProfessionalEducationN",
        "ProfessionalWorkstation", "ProfessionalWorkstationN",
        "Enterprise", "EnterpriseS", "EnterpriseN", "EnterpriseSN",
        "Education", "EducationN", "IoTEnterprise", "IoTEnterpriseS"
    };

    private readonly ILogger<WindowsConsoleShellRegistrationService> _logger;

    public WindowsConsoleShellRegistrationService(ILogger<WindowsConsoleShellRegistrationService> logger)
    {
        _logger = logger;
    }

    public Task<ConsoleShellSupportStatus> GetSupportStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var edition = ReadEditionId();
            if (!SupportedEditions.Contains(edition))
            {
                return Task.FromResult(new ConsoleShellSupportStatus(false, edition, "UnsupportedWindowsEdition"));
            }

            if (!IsSupportedWindowsVersion())
            {
                return Task.FromResult(new ConsoleShellSupportStatus(false, edition, "UnsupportedWindowsVersion"));
            }

            return Task.FromResult(new ConsoleShellSupportStatus(true, edition, "SupportedPerUserCustomShellPolicy"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "[CONSOLE SHELL] RegistrationScope=CurrentUserPolicy Result=Unsupported Reason=SupportDetectionFailed");
            return Task.FromResult(new ConsoleShellSupportStatus(false, "Unknown", "SupportDetectionFailed"));
        }
    }

    public async Task<ConsoleShellRegistrationResult> ConfigureAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            if (enabled)
            {
                var support = await GetSupportStatusAsync(cancellationToken);
                if (!support.IsSupported)
                {
                    _logger.LogWarning(
                        "[CONSOLE SHELL] Enabled=True RegistrationScope=CurrentUserPolicy Edition={Edition} Result=Unsupported Reason={Reason}",
                        support.Edition,
                        support.Reason);
                    return new ConsoleShellRegistrationResult(false, false, support.Reason);
                }
            }

            var shellCommand = $"\"{ResolveExecutablePath()}\" --console-shell-startup";
            using var key = Registry.CurrentUser.CreateSubKey(CustomShellPolicyPath, writable: true)
                ?? throw new InvalidOperationException("The current-user Custom Shell policy key could not be opened.");
            var existing = key.GetValue(CustomShellValueName) as string;

            if (enabled)
            {
                if (!string.IsNullOrWhiteSpace(existing) && !IsOwnedGameShellCommand(existing))
                {
                    _logger.LogWarning(
                        "[CONSOLE SHELL] Enabled=True RegistrationScope=CurrentUserPolicy Result=Conflict Reason=ExistingCustomShellPolicy");
                    return new ConsoleShellRegistrationResult(false, false, "ExistingCustomShellPolicy");
                }

                key.SetValue(CustomShellValueName, shellCommand, RegistryValueKind.String);
                var configured = key.GetValue(CustomShellValueName) as string;
                var success = string.Equals(configured, shellCommand, StringComparison.Ordinal);
                _logger.LogInformation(
                    "[CONSOLE SHELL] Enabled=True RegistrationScope=CurrentUserPolicy ConfiguredShell=GameShell.exe Result={Result}",
                    success ? "Success" : "VerificationFailed");
                return new ConsoleShellRegistrationResult(success, success, success ? "ConsoleShellEnabled" : "ShellPolicyVerificationFailed");
            }

            if (!string.IsNullOrWhiteSpace(existing) && !IsOwnedGameShellCommand(existing))
            {
                _logger.LogWarning(
                    "[SHELL DISABLE] RegistrationScope=CurrentUserPolicy Result=Conflict Reason=ExistingCustomShellPolicy");
                return new ConsoleShellRegistrationResult(false, true, "ExistingCustomShellPolicy");
            }

            key.DeleteValue(CustomShellValueName, throwOnMissingValue: false);
            var removed = key.GetValue(CustomShellValueName) is null;
            _logger.LogInformation(
                "[SHELL DISABLE] RegistrationScope=CurrentUserPolicy RestoredShell=explorer.exe Result={Result}",
                removed ? "Success" : "VerificationFailed");
            return new ConsoleShellRegistrationResult(removed, false, removed ? "ConsoleShellDisabled" : "ShellPolicyVerificationFailed");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[CONSOLE SHELL] Enabled={Enabled} RegistrationScope=CurrentUserPolicy Result=Failed",
                enabled);
            return new ConsoleShellRegistrationResult(false, !enabled, "ShellPolicyWriteFailed");
        }
    }

    private static bool IsOwnedGameShellCommand(string command)
    {
        const string startupArgument = "--console-shell-startup";
        var trimmed = command.Trim();
        if (!trimmed.EndsWith(startupArgument, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var executableToken = trimmed[..^startupArgument.Length].Trim().Trim('"');
        return string.Equals(
            Path.GetFileName(executableToken),
            "GameShell.exe",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedWindowsVersion()
    {
        var version = Environment.OSVersion.Version;
        if (version.Major > 10 || version.Build >= 22000)
        {
            return true;
        }

        if (version.Major < 10 || version.Build < 19041)
        {
            return false;
        }

        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var updateBuildRevision = key?.GetValue("UBR") is int ubr ? ubr : 0;
        return version.Build >= 19044 || updateBuildRevision >= 1202;
    }

    private static string ReadEditionId()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        return key?.GetValue("EditionID") as string ?? "Unknown";
    }

    private static string ResolveExecutablePath()
    {
        var appHostPath = Path.Combine(AppContext.BaseDirectory, "GameShell.exe");
        if (File.Exists(appHostPath))
        {
            return appHostPath;
        }

        return Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? Assembly.GetEntryAssembly()?.Location
            ?? throw new InvalidOperationException("GameShell executable path is unavailable.");
    }
}
