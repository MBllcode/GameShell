using System.Threading;

namespace GameShell.Core.Services;

public static class WorkModeTransitionContext
{
    private static readonly AsyncLocal<string?> CurrentTransition = new();

    public static string? CurrentId => CurrentTransition.Value;

    public static IDisposable Begin(string transitionId)
    {
        var previous = CurrentTransition.Value;
        CurrentTransition.Value = transitionId;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? _previous;

        public Scope(string? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            CurrentTransition.Value = _previous;
        }
    }
}