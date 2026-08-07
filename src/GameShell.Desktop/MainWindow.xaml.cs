using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Desktop.WebView;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace GameShell.Desktop;

public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private readonly WebViewBridge _webViewBridge;
    private readonly ISettingsService _settingsService;
    private bool _isExitRequested;

    public MainWindow(
        ILogger<MainWindow> logger,
        WebViewBridge webViewBridge,
        ISettingsService settingsService)
    {
        _logger = logger;
        _webViewBridge = webViewBridge;
        _settingsService = settingsService;
        InitializeComponent();
        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ApplySavedWindowBoundsAsync();

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

    private async Task ApplySavedWindowBoundsAsync()
    {
        var settings = await _settingsService.LoadAsync();
        if (settings.Ui.WindowWidth is null || settings.Ui.WindowHeight is null)
        {
            FitWindowToPrimaryWorkArea();
            CenterOnPrimaryScreen();
            return;
        }

        Width = Math.Max(MinWidth, settings.Ui.WindowWidth.Value);
        Height = Math.Max(MinHeight, settings.Ui.WindowHeight.Value);
        FitWindowToPrimaryWorkArea();

        if (settings.Ui.WindowLeft is null || settings.Ui.WindowTop is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        Left = settings.Ui.WindowLeft.Value;
        Top = settings.Ui.WindowTop.Value;
        EnsureWindowVisible();
    }

    private void EnsureWindowVisible()
    {
        FitWindowToPrimaryWorkArea();

        var windowRectangle = new System.Drawing.Rectangle(
            (int)Math.Round(Left),
            (int)Math.Round(Top),
            (int)Math.Round(Width),
            (int)Math.Round(Height));

        if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(windowRectangle)))
        {
            return;
        }

        CenterOnPrimaryScreen();
    }

    private void FitWindowToPrimaryWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        var maxWidth = Math.Max(MinWidth, workArea.Width * 0.92);
        var maxHeight = Math.Max(MinHeight, workArea.Height * 0.88);
        Width = Math.Min(Math.Max(MinWidth, Width), maxWidth);
        Height = Math.Min(Math.Max(MinHeight, Height), maxHeight);
    }

    private void CenterOnPrimaryScreen()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.AllScreens.First().WorkingArea;
        Left = area.Left + Math.Max(0, (area.Width - Width) / 2.0);
        Top = area.Top + Math.Max(0, (area.Height - Height) / 2.0);
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

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        await SaveWindowBoundsAsync();
    }
}
