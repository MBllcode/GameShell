namespace GameShell.Core.Models;

public sealed record ExternalProcessLaunchRequest(
    string ExecutablePath,
    string Arguments = "",
    string? WorkingDirectory = null,
    bool UseShellExecute = false);
