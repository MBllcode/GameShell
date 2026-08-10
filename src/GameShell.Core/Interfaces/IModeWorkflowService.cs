using GameShell.Core.Models;

namespace GameShell.Core.Interfaces;

public interface IModeWorkflowService
{
    Task<ModeWorkflowResult> PrepareWorkWorkflowAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);

    Task<ModeWorkflowResult> PrepareGamingWorkflowAsync(
        AppSettings settings,
        GameShellMode mode,
        CancellationToken cancellationToken = default);

    Task<ModeWorkflowResult> CompleteGamingWorkflowAsync(
        AppSettings settings,
        GameShellMode mode,
        CancellationToken cancellationToken = default);

    Task<ModeWorkflowResult> RunGamingWorkflowAsync(
        AppSettings settings,
        GameShellMode mode,
        CancellationToken cancellationToken = default);

    Task<ModeWorkflowResult> RunWorkWorkflowAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);
}

public sealed record ModeWorkflowResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static ModeWorkflowResult Ok(string? message = null)
    {
        return new ModeWorkflowResult
        {
            Success = true,
            Message = message
        };
    }

    public static ModeWorkflowResult Fail(string message)
    {
        return new ModeWorkflowResult
        {
            Success = false,
            Message = message
        };
    }
}
