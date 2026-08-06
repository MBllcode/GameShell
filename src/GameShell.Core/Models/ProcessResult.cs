namespace GameShell.Core.Models;

public sealed class ProcessResult
{
    public string FileName { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public int? ExitCode { get; init; }
    public bool TimedOut { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public long DurationMs { get; init; }
}
