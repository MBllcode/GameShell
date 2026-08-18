using System.Diagnostics;
using System.IO;
using System.Reflection;
using GameShell.Core.Models;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.Services;

/// <summary>
/// Owns Windows startup registration and shortcut side effects.
/// </summary>
public sealed class WindowsIntegrationService : IWindowsIntegrationService
{
    private const string LegacyRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string LegacyRunValueName = "GameShell";

    private readonly ILogger<WindowsIntegrationService> _logger;

    public WindowsIntegrationService(ILogger<WindowsIntegrationService> logger)
    {
        _logger = logger;
    }

    public void Apply(AppSettings settings)
    {
        var startupRegistration = ResolveStartupRegistration();

        ApplyStartup(
            settings.Startup.StartWithWindows,
            startupRegistration.ShortcutTargetPath,
            startupRegistration.ShortcutArguments);
        _logger.LogInformation(
            "Windows integration applied. StartWithWindows={StartWithWindows}, RunCommand={RunCommand}, BaseDirectory={BaseDirectory}.",
            settings.Startup.StartWithWindows,
            startupRegistration.RunCommand,
            AppContext.BaseDirectory);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "GameShellShortcut.ico");
        ApplyShortcut(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "GameShell.lnk",
            settings.Startup.CreateDesktopShortcut,
            startupRegistration.ShortcutTargetPath,
            startupRegistration.ShortcutArguments,
            iconPath);
        ApplyShortcut(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Path.Combine("Programs", "GameShell.lnk"),
            settings.Startup.CreateStartMenuShortcut,
            startupRegistration.ShortcutTargetPath,
            startupRegistration.ShortcutArguments,
            iconPath);
    }

    private void ApplyStartup(bool enabled, string targetPath, string arguments)
    {
        try
        {
            if (enabled)
            {
                RegisterLegacyRunValue(targetPath, arguments);
                return;
            }

            RemoveLegacyRunValue();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Windows startup integration could not be updated for the current user."
            );
        }
    }

    private void RegisterLegacyRunValue(string targetPath, string arguments)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(LegacyRunKeyPath, writable: true)
            ?? throw new InvalidOperationException("The current-user startup registry key could not be opened.");
        var command = string.IsNullOrWhiteSpace(arguments)
            ? $"\"{targetPath}\""
            : $"\"{targetPath}\" {arguments}";
        runKey.SetValue(LegacyRunValueName, command, RegistryValueKind.String);
        _logger.LogInformation(
            "Windows startup registered in HKCU Run. ValueName={ValueName}, Command={Command}.",
            LegacyRunValueName,
            command);
    }



    private static void RemoveLegacyRunValue()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(LegacyRunKeyPath, writable: true);
        runKey?.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
    }

    private static void ApplyShortcut(
        string baseDirectory,
        string relativeShortcutPath,
        bool enabled,
        string targetPath,
        string arguments,
        string iconPath)
    {
        var shortcutPath = Path.Combine(baseDirectory, relativeShortcutPath);
        if (!enabled)
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        CreateShortcut(shortcutPath, targetPath, arguments, iconPath);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string iconPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows shortcut service is not available.");

        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Windows shortcut service could not be created.");
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.Arguments = arguments;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
        shortcut.Description = "GameShell";
        shortcut.IconLocation = File.Exists(iconPath) ? $"{iconPath},0" : $"{targetPath},0";
        shortcut.Save();
    }

    private static StartupRegistration ResolveStartupRegistration()
    {
        var processPath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("GameShell executable path is not available.");

        var appHostPath = Path.Combine(AppContext.BaseDirectory, "GameShell.exe");
        if (File.Exists(appHostPath))
        {
            return new StartupRegistration(appHostPath, string.Empty, $"\"{appHostPath}\"");
        }

        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        var processFileName = Path.GetFileName(processPath);
        var isDotnetHost = string.Equals(processFileName, "dotnet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(processFileName, "dotnet.exe", StringComparison.OrdinalIgnoreCase);

        if (isDotnetHost
            && !string.IsNullOrWhiteSpace(entryAssemblyPath)
            && entryAssemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var arguments = $"\"{entryAssemblyPath}\"";
            return new StartupRegistration(processPath, arguments, $"\"{processPath}\" {arguments}");
        }

        return new StartupRegistration(processPath, string.Empty, $"\"{processPath}\"");
    }

    private sealed record StartupRegistration(string ShortcutTargetPath, string ShortcutArguments, string RunCommand);
}
