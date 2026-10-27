using Microsoft.Extensions.Logging;

namespace GameShell.Core.Runtime;

public sealed class TransientOperationStateService : ITransientOperationStateService
{
    private readonly object _syncRoot = new();
    private readonly ILogger<TransientOperationStateService> _logger;
    private TransientOperationState _current = TransientOperationState.None;

    public TransientOperationStateService(ILogger<TransientOperationStateService> logger)
    {
        _logger = logger;
    }

    public TransientOperationState Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _current;
            }
        }
    }

    public event EventHandler<TransientOperationChangedEventArgs>? Changed;

    public string? Begin(
        TransientOperationKind kind,
        TransientOperationPhase phase,
        string? transitionId = null)
    {
        var id = string.IsNullOrWhiteSpace(transitionId)
            ? Guid.NewGuid().ToString("N")
            : transitionId;

        TransientOperationChangedEventArgs? change = null;
        lock (_syncRoot)
        {
            if (_current.IsActive)
            {
                _logger.LogWarning(
                    "[TRANSIENT UI STATE] Ignored begin for {Kind}/{Phase}; current operation is {CurrentKind}/{CurrentPhase} TransitionId={TransitionId}.",
                    kind,
                    phase,
                    _current.Kind,
                    _current.Phase,
                    _current.TransitionId ?? "-");
                return null;
            }

            var next = CreateState(kind, phase, id, DateTimeOffset.Now);
            change = CreateChange(_current, next, "Started");
            _current = next;
        }

        Publish(change);
        return id;
    }

    public bool SetPhase(string transitionId, TransientOperationPhase phase)
    {
        TransientOperationChangedEventArgs? change = null;
        lock (_syncRoot)
        {
            if (!_current.IsActive || !string.Equals(_current.TransitionId, transitionId, StringComparison.Ordinal))
            {
                return false;
            }

            if (_current.Phase == phase)
            {
                return true;
            }

            var next = _current with
            {
                Phase = phase,
                MessageKey = GetMessageKey(phase)
            };
            change = CreateChange(_current, next, "PhaseChanged");
            _current = next;
        }

        Publish(change);
        return true;
    }

    public bool Clear(string transitionId, string reason)
    {
        TransientOperationChangedEventArgs? change = null;
        lock (_syncRoot)
        {
            if (!_current.IsActive || !string.Equals(_current.TransitionId, transitionId, StringComparison.Ordinal))
            {
                return false;
            }

            change = CreateChange(_current, TransientOperationState.None, reason);
            _current = TransientOperationState.None;
        }

        Publish(change);
        return true;
    }

    private void Publish(TransientOperationChangedEventArgs? change)
    {
        if (change is null)
        {
            return;
        }

        _logger.LogInformation(
            "[TRANSIENT UI STATE] TransitionId={TransitionId} OldKind={OldKind} NewKind={NewKind} OldPhase={OldPhase} NewPhase={NewPhase} Reason={Reason}",
            change.NewState.TransitionId ?? change.OldState.TransitionId ?? "-",
            change.OldState.Kind,
            change.NewState.Kind,
            change.OldState.Phase,
            change.NewState.Phase,
            change.Reason);
        Changed?.Invoke(this, change);
    }

    private static TransientOperationChangedEventArgs CreateChange(
        TransientOperationState oldState,
        TransientOperationState newState,
        string reason)
        => new(oldState, newState, reason);

    private static TransientOperationState CreateState(
        TransientOperationKind kind,
        TransientOperationPhase phase,
        string transitionId,
        DateTimeOffset startedAt)
        => new()
        {
            Kind = kind,
            Phase = phase,
            MessageKey = GetMessageKey(phase),
            StartedAt = startedAt,
            TransitionId = transitionId
        };

    private static string? GetMessageKey(TransientOperationPhase phase)
        => phase switch
        {
            TransientOperationPhase.PreparingDisplays => "status.transient.preparingDisplays",
            TransientOperationPhase.ApplyingDisplayTopology => "status.transient.applyingDisplayTopology",
            TransientOperationPhase.SynchronizingDisplays => "status.transient.synchronizingDisplays",
            TransientOperationPhase.ApplyingProfileSettings => "status.transient.applyingProfileSettings",
            TransientOperationPhase.StartingLauncher => "status.transient.startingLauncher",
            TransientOperationPhase.ActivatingLauncher => "status.transient.activatingLauncher",
            TransientOperationPhase.RestoringDisplays => "status.transient.restoringDisplays",
            TransientOperationPhase.RestoringDesktop => "status.transient.restoringDesktop",
            _ => null
        };
}
