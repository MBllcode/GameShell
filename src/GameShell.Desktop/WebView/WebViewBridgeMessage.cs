using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameShell.Desktop.WebView;

public sealed class WebViewBridgeMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = string.Empty;

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }
}
