namespace GameShell.Core.Shortcuts;

public readonly record struct ControllerStatusSnapshot(
    bool Connected,
    string? ControllerId,
    string? Name,
    string Family);

public sealed class ControllerStatusChangeTracker
{
    private readonly Lock _syncRoot = new();
    private ControllerStatusSnapshot? _lastPublished;
    private long _sequence;

    public bool TryRecord(ControllerStatusSnapshot snapshot, out long sequence)
    {
        lock (_syncRoot)
        {
            if (_lastPublished == snapshot)
            {
                sequence = _sequence;
                return false;
            }

            _lastPublished = snapshot;
            sequence = ++_sequence;
            return true;
        }
    }
}