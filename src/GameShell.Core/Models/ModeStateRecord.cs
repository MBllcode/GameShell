namespace GameShell.Core.Models;

public sealed class ModeStateRecord
{
    public string LastRequestedMode { get; set; } = "Unknown";
    public DateTimeOffset? LastCompletedAt { get; set; }
    public string LastResult { get; set; } = "Unknown";
}
