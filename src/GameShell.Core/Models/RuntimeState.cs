namespace GameShell.Core.Models;

public sealed class RuntimeState
{
    public GameShellMode? CurrentMode { get; set; }
    public DisplayLayoutSnapshot? WorkReturnLayout { get; set; }
    public DisplayLayoutSnapshot? PreviousDisplayLayout { get; set; }
    public string LastRequestedMode { get; set; } = "Unknown";
    public DateTimeOffset? LastCompletedAt { get; set; }
    public string LastResult { get; set; } = "Unknown";
    public string LastActiveMode { get; set; } = "Work";
    public string? LastActiveProfileId { get; set; }
}
