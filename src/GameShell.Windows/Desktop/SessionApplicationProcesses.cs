using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GameShell.Windows.Desktop;

internal sealed class SessionApplicationProcesses(ILogger logger)
{
    private readonly Dictionary<int, (DateTime StartedAtUtc, string ExecutablePath, IntPtr Window)> _startedProcesses = [];

    internal static HashSet<int> CaptureExistingProcesses(string executablePath)
    {
        var ids = new HashSet<int>();
        CaptureExecutable(executablePath);
        if (PlayniteSessionApplication.GetAlternateExecutable(executablePath) is { } alternate)
        {
            CaptureExecutable(alternate);
        }

        return ids;

        void CaptureExecutable(string path)
        {
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path)))
            {
                using (process) ids.Add(process.Id);
            }
        }
    }

    internal bool Track(Process process, string executablePath, IReadOnlySet<int> existingProcessIds, IntPtr window = default)
    {
        if (!Path.IsPathFullyQualified(executablePath)
            || process.Id == Environment.ProcessId
            || existingProcessIds.Contains(process.Id)
            || Path.GetFileNameWithoutExtension(executablePath).Equals("explorer", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileNameWithoutExtension(executablePath).Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            _startedProcesses[process.Id] = (process.StartTime.ToUniversalTime(), executablePath, window);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            logger.LogDebug(exception, "Could not verify ownership of session application PID={ProcessId}.", process.Id);
            return false;
        }
    }

    internal void EndSession(bool closeApplications, CancellationToken cancellationToken, Func<Process, IntPtr, bool>? requestClose = null)
    {
        if (!closeApplications)
        {
            _startedProcesses.Clear();
            return;
        }

        // Let the app handle WM_CLOSE, including prompts to save unsaved files.
        foreach (var (processId, started) in _startedProcesses.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.StartTime.ToUniversalTime() == started.StartedAtUtc
                    && string.Equals(process.MainModule?.FileName, started.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    var sent = requestClose is not null ? requestClose(process, started.Window)
                        : PlayniteSessionApplication.TryShutdown(process, started.ExecutablePath)
                            || WindowsWindowManagementService.RequestSessionApplicationClose(process, started.Window);
                    logger.LogInformation("Session application close requested. App={Application} PID={ProcessId} Sent={Sent}.",
                        Path.GetFileName(started.ExecutablePath), processId, sent);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
            {
                logger.LogDebug(exception, "Could not close session application PID={ProcessId}.", processId);
            }

            _startedProcesses.Remove(processId);
        }
    }
}
