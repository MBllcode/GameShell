using GameShell.Core.Services;
using GameShell.Desktop.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class ControllerLoggingTests
{
    [Fact]
    public void RepeatedDisconnectedPollingPublishesNoAdditionalChanges()
    {
        var tracker = new ControllerStatusChangeTracker();
        var disconnected = new ControllerStatusSnapshot(false, null, null, "Generic");

        var changed = 0;
        for (var index = 0; index < 1000; index++)
        {
            if (tracker.TryRecord(disconnected, out long _)) changed++;
        }

        Assert.Equal(1, changed);
    }

    [Fact]
    public void ControllerTransitionsProduceOneChangeEach()
    {
        var tracker = new ControllerStatusChangeTracker();
        var disconnected = new ControllerStatusSnapshot(false, null, null, "Generic");
        var connected = new ControllerStatusSnapshot(true, "42", "DualSense Wireless Controller", "PlayStation");
        var sameConnected = connected;
        var changedType = new ControllerStatusSnapshot(true, "42", "Xbox Wireless Controller", "Xbox");

        Assert.True(tracker.TryRecord(disconnected, out var disconnectedSequence));
        Assert.True(tracker.TryRecord(connected, out var connectedSequence));
        Assert.False(tracker.TryRecord(sameConnected, out _));
        Assert.True(tracker.TryRecord(changedType, out var changedTypeSequence));
        Assert.True(tracker.TryRecord(disconnected, out var finalDisconnectedSequence));
        Assert.Equal((1, 2, 3, 4), (disconnectedSequence, connectedSequence, changedTypeSequence, finalDisconnectedSequence));
    }

    [Fact]
    public void IdenticalErrorsAreSuppressedUntilTheIntervalExpires()
    {
        var limiter = new LogRateLimiter();
        var start = DateTimeOffset.UtcNow;
        var accepted = Enumerable.Range(0, 100)
            .Count(_ => limiter.TryAcquire("SDL error", start, TimeSpan.FromSeconds(60), out _));

        Assert.Equal(1, accepted);
        Assert.True(limiter.TryAcquire("SDL error", start.AddSeconds(60), TimeSpan.FromSeconds(60), out var suppressed));
        Assert.Equal(99, suppressed);
    }

    [Fact]
    public void FileLoggerDoesNotPropagateFormatterFailure()
    {
        using var provider = new FileLoggerProvider();
        var logger = provider.CreateLogger("test");

        var exception = Record.Exception(() => logger.Log(
            LogLevel.Information,
            new EventId(1),
            new object(),
            null,
            (_, _) => throw new IOException("simulated logger failure")));

        Assert.Null(exception);
    }
}