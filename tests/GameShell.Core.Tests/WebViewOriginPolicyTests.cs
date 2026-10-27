using GameShell.Desktop.WebView;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class WebViewOriginPolicyTests
{
    [Theory]
    [InlineData("https://app.gameshell.local/index.html", true)]
    [InlineData("https://app.gameshell.local:443/index.html#settings", true)]
    [InlineData("https://APP.GAMESHELL.LOCAL/", true)]
    [InlineData("http://app.gameshell.local/", false)]
    [InlineData("https://app.gameshell.local:8443/", false)]
    [InlineData("https://app.gameshell.local.evil.example/", false)]
    [InlineData("https://app.gameshell.local@evil.example/", false)]
    [InlineData("https://user@app.gameshell.local/", false)]
    [InlineData("file:///C:/index.html", false)]
    [InlineData("data:text/html,test", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("about:blank", false)]
    [InlineData(null, false)]
    public void MessagesRequireExactAppOrigin(string? source, bool trusted)
    {
        Assert.Equal(trusted, WebViewOriginPolicy.IsTrusted(source));
        Assert.Equal(trusted || source == "about:blank", WebViewOriginPolicy.CanNavigate(source));
    }
}
