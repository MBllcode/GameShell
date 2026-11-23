using System.ComponentModel;
using System.Diagnostics;

namespace GameShell.Windows.Desktop;

internal static class PlayniteSessionApplication
{
    private const string Desktop = "Playnite.DesktopApp.exe";
    private const string Fullscreen = "Playnite.FullscreenApp.exe";

    internal static bool IsPlaynite(string path) => GetAlternateExecutable(path) is not null;

    internal static string? GetAlternateExecutable(string executable)
    {
        if (!Path.IsPathFullyQualified(executable)) return null;
        var name = Path.GetFileName(executable);
        var directory = Path.GetDirectoryName(executable);
        if (directory is null) return null;

        if (string.Equals(name, Desktop, StringComparison.OrdinalIgnoreCase)) return Path.Combine(directory, Fullscreen);
        if (string.Equals(name, Fullscreen, StringComparison.OrdinalIgnoreCase)) return Path.Combine(directory, Desktop);
        return null;
    }

    internal static bool TryShutdown(Process sessionProcess, string executable)
    {
        if (!IsPlaynite(executable)) return false;
        using var current = Process.GetCurrentProcess();
        if (sessionProcess.SessionId != current.SessionId) return false;

        // Playnite's shutdown command addresses the instance via IPC. If another
        // instance is present in this Windows session, use a targeted window close.
        var instances = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Desktop))
            .Concat(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Fullscreen))).ToArray();
        try
        {
            var sessionId = sessionProcess.SessionId;
            if (instances.Any(instance => instance.Id != sessionProcess.Id
                && !instance.HasExited && instance.SessionId == sessionId))
            {
                return false;
            }

            using var command = Process.Start(new ProcessStartInfo(executable, "--shutdown")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(executable)!
            });
            return command is not null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            return false;
        }
        finally
        {
            foreach (var instance in instances) instance.Dispose();
        }
    }
}
