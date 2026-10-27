using GameShell.Desktop.Bridge;
using System.Text.Json;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class WebViewCommandRouterTests
{
    [Fact]
    public async Task DispatchAsync_InvokesHandlerWithPayload()
    {
        using var document = JsonDocument.Parse("{\"value\":42}");
        var router = new WebViewCommandRouter(new Dictionary<string, Func<JsonElement?, Task<object>>>
        {
            ["test"] = payload => Task.FromResult<object>(payload!.Value.GetProperty("value").GetInt32())
        });

        var result = await router.DispatchAsync("test", document.RootElement);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task DispatchAsync_RejectsUnsupportedCommand()
    {
        var router = new WebViewCommandRouter(new Dictionary<string, Func<JsonElement?, Task<object>>>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => router.DispatchAsync("missing", null));

        Assert.Contains("missing", exception.Message);
    }
}
