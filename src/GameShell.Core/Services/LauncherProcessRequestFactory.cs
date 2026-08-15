using GameShell.Core.Models;

namespace GameShell.Core.Services;

/// <summary>
/// Converts persisted launcher settings into a platform-neutral process request.
/// It intentionally contains launcher policy only; token and process creation stay in GameShell.Windows.
/// </summary>
public static class LauncherProcessRequestFactory
{
    private const string SteamBigPictureUri = "steam://open/bigpicture";
    private const string XboxAppShellTarget = "shell:AppsFolder\\Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App";

    public static ExternalProcessLaunchRequest Create(LauncherSettings launcher)
    {
        if (launcher.Type.Equals("Steam", StringComparison.OrdinalIgnoreCase))
        {
            var executablePath = string.IsNullOrWhiteSpace(launcher.ExecutablePath)
                ? SteamBigPictureUri
                : PathExpander.Expand(launcher.ExecutablePath);

            return CreateShellRequest(executablePath, launcher.Arguments);
        }

        if (launcher.Type.Equals("XboxApp", StringComparison.OrdinalIgnoreCase))
        {
            return new ExternalProcessLaunchRequest(
                "explorer.exe",
                XboxAppShellTarget,
                UseShellExecute: false);
        }

        return new ExternalProcessLaunchRequest(
            PathExpander.Expand(launcher.ExecutablePath),
            launcher.Arguments,
            UseShellExecute: false);
    }

    private static ExternalProcessLaunchRequest CreateShellRequest(string target, string arguments)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return new ExternalProcessLaunchRequest(
                "explorer.exe",
                JoinArguments(QuoteArgument(target), arguments),
                UseShellExecute: false);
        }

        return new ExternalProcessLaunchRequest(target, arguments, UseShellExecute: false);
    }

    private static string JoinArguments(string first, string second)
    {
        return string.IsNullOrWhiteSpace(second) ? first : $"{first} {second}";
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
