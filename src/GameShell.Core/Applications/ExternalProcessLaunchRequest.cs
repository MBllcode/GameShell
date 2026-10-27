namespace GameShell.Core.Applications;

public sealed record ExternalProcessLaunchRequest(
    string ExecutablePath,
    string Arguments = "",
    string? WorkingDirectory = null,
    bool UseShellExecute = false);
