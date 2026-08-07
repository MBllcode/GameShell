namespace GameShell.Core.Models;

public sealed record CompletedControllerCapture
{
    public required Guid SessionId { get; init; }
    public required string ProfileId { get; init; }
    public required IReadOnlyList<ControllerButton> Buttons { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
}