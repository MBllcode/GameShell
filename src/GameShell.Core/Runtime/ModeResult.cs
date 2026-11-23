namespace GameShell.Core.Runtime;

public sealed class ModeResult
{
    public ModeResultStatus Status { get; init; }
    public ModeState CurrentState { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Warning { get; init; }
    public long DurationMs { get; init; }

    public bool Success => Status is ModeResultStatus.Success or ModeResultStatus.NoChanges;
}
