namespace GameShell.Core.Displays;

public sealed record DisplayTransitionResult
{
    public bool Success { get; init; }
    public bool RolledBack { get; init; }
    public string? ErrorCode { get; init; }
    public string? UserMessage { get; init; }
    public DisplayLayoutSnapshot? TargetLayout { get; init; }

    public static DisplayTransitionResult Ok(
        string? message = null,
        DisplayLayoutSnapshot? targetLayout = null)
    {
        return new DisplayTransitionResult
        {
            Success = true,
            UserMessage = message,
            TargetLayout = targetLayout
        };
    }

    public static DisplayTransitionResult Fail(string errorCode, string userMessage, bool rolledBack = false)
    {
        return new DisplayTransitionResult
        {
            Success = false,
            RolledBack = rolledBack,
            ErrorCode = errorCode,
            UserMessage = userMessage
        };
    }
}
