using System.Text.Json.Serialization;

namespace GameShell.Desktop.Bridge;

public sealed class WebViewBridgeResponse
{
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public object? Data { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
