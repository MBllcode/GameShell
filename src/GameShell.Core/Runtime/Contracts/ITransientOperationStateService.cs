
namespace GameShell.Core.Runtime;

public interface ITransientOperationStateService
{
    TransientOperationState Current { get; }

    event EventHandler<TransientOperationChangedEventArgs>? Changed;

    string? Begin(
        TransientOperationKind kind,
        TransientOperationPhase phase,
        string? transitionId = null);

    bool SetPhase(
        string transitionId,
        TransientOperationPhase phase);

    bool Clear(
        string transitionId,
        string reason);
}

public sealed class TransientOperationChangedEventArgs : EventArgs
{
    public TransientOperationChangedEventArgs(
        TransientOperationState oldState,
        TransientOperationState newState,
        string reason)
    {
        OldState = oldState;
        NewState = newState;
        Reason = reason;
    }

    public TransientOperationState OldState { get; }
    public TransientOperationState NewState { get; }
    public string Reason { get; }
}
