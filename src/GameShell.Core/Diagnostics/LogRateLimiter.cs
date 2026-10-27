namespace GameShell.Core.Diagnostics;

public sealed class LogRateLimiter
{
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public bool TryAcquire(string key, DateTimeOffset now, TimeSpan interval, out int suppressedCount)
    {
        lock (_syncRoot)
        {
            if (!_entries.TryGetValue(key, out var entry) || now - entry.LastLoggedAt >= interval)
            {
                suppressedCount = entry?.SuppressedCount ?? 0;
                _entries[key] = new Entry(now, 0);
                return true;
            }

            _entries[key] = entry with { SuppressedCount = entry.SuppressedCount + 1 };
            suppressedCount = 0;
            return false;
        }
    }

    private sealed record Entry(DateTimeOffset LastLoggedAt, int SuppressedCount);
}