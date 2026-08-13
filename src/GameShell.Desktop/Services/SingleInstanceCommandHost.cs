using System.Windows.Threading;

namespace GameShell.Desktop.Services;

/// <summary>
/// Owns the lifetime of named command events and marshals their callbacks to WPF.
/// </summary>
internal sealed class SingleInstanceCommandHost : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly List<EventWaitHandle> _events = [];
    private readonly List<RegisteredWaitHandle> _registrations = [];
    private bool _disposed;

    public SingleInstanceCommandHost(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Register(string eventName, Func<Task> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var commandEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        var registration = ThreadPool.RegisterWaitForSingleObject(
            commandEvent,
            (_, _) => _dispatcher.BeginInvoke(action),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        _events.Add(commandEvent);
        _registrations.Add(registration);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var registration in _registrations)
        {
            registration.Unregister(null);
        }

        foreach (var commandEvent in _events)
        {
            commandEvent.Dispose();
        }

        _registrations.Clear();
        _events.Clear();
    }
}
