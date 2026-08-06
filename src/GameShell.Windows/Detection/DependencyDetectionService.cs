using System.Diagnostics;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using Microsoft.Win32;

namespace GameShell.Windows.Detection;

public sealed class DependencyDetectionService : IDependencyDetectionService
{
    private const string PlayniteDownloadUrl = "https://playnite.link/download/PlayniteInstaller.exe";
    private const string SteamDownloadUrl = "https://store.steampowered.com/about/";
    private const string XboxAppDownloadUrl = "https://www.xbox.com/en-us/apps/xbox-app-on-pc";

    public Task<IReadOnlyList<DependencyStatus>> DetectAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<DependencyStatus> dependencies =
        [
            DetectPlaynite(settings.Launcher.ExecutablePath),
            DetectSteam(),
            DetectXboxApp()
        ];

        return Task.FromResult(dependencies);
    }

    private static DependencyStatus DetectPlaynite(string configuredPath)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = FirstExistingPath(
            Environment.ExpandEnvironmentVariables(configuredPath),
            Path.Combine(localAppData, "Playnite", "Playnite.FullscreenApp.exe"),
            Path.Combine(localAppData, "Playnite", "Playnite.DesktopApp.exe"),
            @"C:\Program Files\Playnite\Playnite.FullscreenApp.exe");

        path ??= FindUninstallInstallLocation("Playnite", "Playnite.FullscreenApp.exe");

        return CreateExecutableStatus(
            name: "Playnite",
            isRequired: false,
            path,
            detectedMessage: "Wykryto Playnite.",
            missingMessage: "Playnite jest opcjonalny.",
            downloadUrl: PlayniteDownloadUrl);
    }

    private static DependencyStatus DetectSteam()
    {
        var path = ReadRegistryString(
            RegistryHive.CurrentUser,
            RegistryView.Default,
            @"Software\Valve\Steam",
            "SteamExe");

        path ??= FirstExistingPath(
            @"C:\Program Files (x86)\Steam\steam.exe",
            @"C:\Program Files\Steam\steam.exe");

        return CreateExecutableStatus(
            name: "Steam",
            isRequired: false,
            path,
            detectedMessage: "Wykryto Steam.",
            missingMessage: "Steam jest opcjonalny.",
            downloadUrl: SteamDownloadUrl);
    }

    private static DependencyStatus DetectXboxApp()
    {
        var packageFullName = FindAppxPackageFullName("Microsoft.GamingApp");
        if (!string.IsNullOrWhiteSpace(packageFullName))
        {
            return new DependencyStatus
            {
                Name = "Xbox App",
                IsRequired = false,
                IsDetected = true,
                DetectedPath = packageFullName,
                Version = null,
                Message = "Wykryto Xbox App.",
                DownloadUrl = XboxAppDownloadUrl
            };
        }

        return new DependencyStatus
        {
            Name = "Xbox App",
            IsRequired = false,
            IsDetected = false,
            DetectedPath = null,
            Version = null,
            Message = "Xbox App jest opcjonalna.",
            DownloadUrl = XboxAppDownloadUrl
        };
    }

    private static DependencyStatus CreateExecutableStatus(
        string name,
        bool isRequired,
        string? path,
        string detectedMessage,
        string missingMessage,
        string downloadUrl)
    {
        var exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);

        return new DependencyStatus
        {
            Name = name,
            IsRequired = isRequired,
            IsDetected = exists,
            DetectedPath = exists ? path : null,
            Version = exists ? FileVersionInfo.GetVersionInfo(path!).ProductVersion : null,
            Message = exists ? detectedMessage : missingMessage,
            DownloadUrl = downloadUrl
        };
    }

    private static string? FirstExistingPath(params string[] candidates)
    {
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindUninstallInstallLocation(string displayNameContains, string executableName)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            var path = FindUninstallInstallLocation(RegistryHive.LocalMachine, view, displayNameContains, executableName);
            if (path is not null)
            {
                return path;
            }
        }

        return FindUninstallInstallLocation(RegistryHive.CurrentUser, RegistryView.Default, displayNameContains, executableName);
    }

    private static string? FindUninstallInstallLocation(
        RegistryHive hive,
        RegistryView view,
        string displayNameContains,
        string executableName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

        if (uninstall is null)
        {
            return null;
        }

        foreach (var subKeyName in uninstall.GetSubKeyNames())
        {
            using var appKey = uninstall.OpenSubKey(subKeyName);
            var displayName = appKey?.GetValue("DisplayName") as string;

            if (displayName is null || !displayName.Contains(displayNameContains, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var installLocation = appKey?.GetValue("InstallLocation") as string;
            if (string.IsNullOrWhiteSpace(installLocation))
            {
                continue;
            }

            var executablePath = Path.Combine(installLocation, executableName);
            if (File.Exists(executablePath))
            {
                return executablePath;
            }
        }

        return null;
    }

    private static string? ReadRegistryString(RegistryHive hive, RegistryView view, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindAppxPackageFullName(string packageName)
    {
        try
        {
            var packagesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages");

            return Directory.Exists(packagesPath)
                ? Directory.EnumerateDirectories(packagesPath, $"{packageName}_*").Select(Path.GetFileName).FirstOrDefault()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
