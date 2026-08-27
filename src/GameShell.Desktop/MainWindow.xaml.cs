using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using GameShell.Core.Interfaces;
using GameShell.Desktop.Services;
using GameShell.Desktop.WebView;
using GameShell.Windows.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace GameShell.Desktop;

public partial class MainWindow : Window
{
    private const int WmDpiChanged = 0x02E0;
    private readonly ILogger<MainWindow> _logger;
    private readonly WebViewBridge _webViewBridge;
    private readonly ISettingsService _settingsService;
    private readonly IWindowPlacementService _windowPlacementService;
    private bool _presentationPrepared;
    private bool _isExitRequested;
    private HwndSource? _hwndSource;
    private bool _layoutLogScheduled;

    public MainWindow(
        ILogger<MainWindow> logger,
        WebViewBridge webViewBridge,
        ISettingsService settingsService,
        IWindowPlacementService windowPlacementService)
    {
        _logger = logger;
        _webViewBridge = webViewBridge;
        _settingsService = settingsService;
        _windowPlacementService = windowPlacementService;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        ContentRendered += OnContentRendered;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameShell",
                "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                        _logger.LogInformation("WebView2 environment created");
            await WebView.EnsureCoreWebView2Async(webViewEnvironment);
            _logger.LogInformation("WebView2 initialized");
            _webViewBridge.Attach(WebView.CoreWebView2);
            var webRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var indexPath = Path.Combine(webRootPath, "index.html");

            if (File.Exists(indexPath))
            {
                WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.gameshell.local",
                    webRootPath,
                    CoreWebView2HostResourceAccessKind.Allow);

                WebView.Source = new Uri("https://app.gameshell.local/index.html");
            }
            else
            {
                WebView.NavigateToString("""
                <!doctype html>
                <html lang="en">
                  <head>
                    <meta charset="utf-8">
                    <title>GameShell</title>
                    <style>
                      body {
                        margin: 0;
                        min-height: 100vh;
                        display: grid;
                        place-items: center;
                        background: #0e1116;
                        color: #f4f7fb;
                        font-family: "Segoe UI", sans-serif;
                      }
                    </style>
                  </head>
                  <body>
                    <main>
                      <h1>GameShell</h1>
                      <p>React UI has not been built yet. Run build.ps1.</p>
                    </main>
                  </body>
                </html>
                """);
                        }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebView2");
            MessageBox.Show("GameShell could not initialize WebView2.", "GameShell", MessageBoxButton.OK, MessageBoxImage.Error);
        }

    }

    public async Task PrepareForPresentationAsync(CancellationToken cancellationToken = default)
    {
        if (!_presentationPrepared)
        {
            await _windowPlacementService.PrepareForPresentationAsync(this, cancellationToken);
            _presentationPrepared = true;
            return;
        }

        await _windowPlacementService.ReconcileAsync(this, cancellationToken);
    }

    public async Task ReconcileWindowPlacementAsync(CancellationToken cancellationToken = default)
    {
        await _windowPlacementService.ReconcileAsync(this, cancellationToken);
        LogWebViewLayout("TopologySettled");
    }

    public Task SaveBoundsForApplicationExitAsync() => SaveWindowBoundsAsync();

    public void AllowApplicationExit()
    {
        _isExitRequested = true;
    }

    private async Task SaveWindowBoundsAsync()
    {
        if (WindowState is WindowState.Minimized)
        {
            return;
        }

        try
        {
            var settings = await _settingsService.LoadAsync();
            var bounds = RestoreBounds;
            settings.Ui.WindowLeft = bounds.Left;
            settings.Ui.WindowTop = bounds.Top;
            settings.Ui.WindowWidth = Math.Max(MinWidth, bounds.Width);
            settings.Ui.WindowHeight = Math.Max(MinHeight, bounds.Height);
            var dpi = GetDpiScale();
            settings.Ui.WindowDpiScaleX = dpi.X;
            settings.Ui.WindowDpiScaleY = dpi.Y;
            await _settingsService.SaveAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save window bounds.");
        }
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        e.Cancel = true;

        try
        {
            var settings = await _settingsService.LoadAsync();
            if (settings.Ui.CloseToTray)
            {
                await SaveWindowBoundsAsync();
                Hide();
                return;
            }

            _isExitRequested = true;
            await SaveWindowBoundsAsync();
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read close-to-tray setting. Hiding window.");
            await SaveWindowBoundsAsync();
            Hide();
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        _hwndSource?.AddHook(WindowMessageHook);
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        LogWebViewLayout("Shown");
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmDpiChanged)
        {
            ScheduleLayoutLog("DpiChanged");
        }

        return IntPtr.Zero;
    }

    private void ScheduleLayoutLog(string reason)
    {
        if (_layoutLogScheduled)
        {
            return;
        }

        _layoutLogScheduled = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _layoutLogScheduled = false;
            LogWebViewLayout(reason);
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void LogWebViewLayout(string reason)
    {
        if (!IsLoaded || !IsVisible)
        {
            return;
        }

        var dpi = GetDpiScale();
        var clientPixelSize = "-";
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero && GetClientRect(handle, out var clientRect))
        {
            clientPixelSize = $"{clientRect.Right - clientRect.Left}x{clientRect.Bottom - clientRect.Top}";
        }

        _logger.LogInformation(
            "[WEBVIEW LAYOUT] Reason={Reason} WindowDipSize={WindowDipSize} ClientPixelSize={ClientPixelSize} WebViewDipSize={WebViewDipSize} DpiScale={DpiScale}",
            reason,
            $"{ActualWidth:F1}x{ActualHeight:F1}",
            clientPixelSize,
            $"{WebView.ActualWidth:F1}x{WebView.ActualHeight:F1}",
            $"{dpi.X:F2}x{dpi.Y:F2}");
    }

    private DpiScaleFactor GetDpiScale()
    {
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformToDevice;
        if (transform is Matrix matrix && matrix.M11 > 0 && matrix.M22 > 0)
        {
            return new DpiScaleFactor(matrix.M11, matrix.M22);
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        return new DpiScaleFactor(dpi.DpiScaleX, dpi.DpiScaleY);
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _hwndSource?.RemoveHook(WindowMessageHook);
        if (_isExitRequested)
        {
            return;
        }

        await SaveWindowBoundsAsync();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr windowHandle, out NativeRect rectangle);
}
