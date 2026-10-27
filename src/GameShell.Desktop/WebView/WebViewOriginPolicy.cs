namespace GameShell.Desktop.WebView;

public static class WebViewOriginPolicy
{
    public const string AppHost = "app.gameshell.local";

    public static bool IsTrusted(string? source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, AppHost, StringComparison.OrdinalIgnoreCase)
        && uri.Port == 443
        && string.IsNullOrEmpty(uri.UserInfo);

    // NavigateToString uses about:blank for the local missing-assets error page.
    // It can be displayed but cannot send privileged bridge commands.
    public static bool CanNavigate(string? source) =>
        IsTrusted(source) || string.Equals(source, "about:blank", StringComparison.Ordinal);
}
