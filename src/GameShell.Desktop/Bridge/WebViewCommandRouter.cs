using System.Text.Json;

namespace GameShell.Desktop.Bridge;

/// <summary>
/// Routes validated WebView command names to application handlers.
/// It deliberately knows nothing about WebView2, JSON serialization or domain services.
/// </summary>
public sealed class WebViewCommandRouter
{
    private readonly IReadOnlyDictionary<string, Func<JsonElement?, Task<object>>> _handlers;

    public WebViewCommandRouter(IReadOnlyDictionary<string, Func<JsonElement?, Task<object>>> handlers)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    public bool IsSupported(string commandName) => _handlers.ContainsKey(commandName);

    public Task<object> DispatchAsync(string commandName, JsonElement? payload)
    {
        if (!_handlers.TryGetValue(commandName, out var handler))
        {
            throw new InvalidOperationException($"Unsupported command: {commandName}.");
        }

        return handler(payload);
    }
}
