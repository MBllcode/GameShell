using GameShell.Core.Runtime;
using GameShell.Core.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameShell.Desktop.RemoteControl;

public sealed class RemoteControlHost : IAsyncDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IModeCommandService _modeCommandService;
    private readonly ILogger<RemoteControlHost> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private WebApplication? _application;
    private CancellationTokenSource? _applicationCancellation;

    public RemoteControlHost(
        ISettingsService settingsService,
        IModeCommandService modeCommandService,
        ILogger<RemoteControlHost> logger,
        ILoggerFactory loggerFactory)
    {
        _settingsService = settingsService;
        _modeCommandService = modeCommandService;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public bool IsRunning => _application is not null;
    public int? BoundPort { get; private set; }

    public async Task StartAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.RemoteControl.Enabled)
        {
            _logger.LogInformation(
                "[REMOTE CONTROL HOST] Enabled=False Port={Port} PrivateNetworkOnly={PrivateNetworkOnly} Result=Disabled",
                settings.RemoteControl.Port,
                settings.RemoteControl.AllowPrivateNetworkOnly);
            return;
        }

        if (_application is not null)
        {
            return;
        }

        var options = RemoteControlOptions.FromSettings(settings.RemoteControl);
        _applicationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        WebApplication? application = null;

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(RemoteControlHost).Assembly.GetName().Name
            });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(serverOptions => serverOptions.ListenAnyIP(options.Port));

            application = builder.Build();
            var handler = new RemoteControlCommandHandler(
                _settingsService,
                _modeCommandService,
                _loggerFactory.CreateLogger<RemoteControlCommandHandler>(),
                _applicationCancellation.Token);

            application.Use(async (context, next) =>
            {
                if (!context.Request.Path.StartsWithSegments("/api/v1"))
                {
                    await next();
                    return;
                }

                var access = RemoteControlAuthentication.CheckRequest(context, options);
                if (!access.Allowed)
                {
                    context.Response.StatusCode = access.StatusCode;
                    await context.Response.WriteAsJsonAsync(new { error = access.Error });
                    return;
                }

                await next();
            });

            application.MapGet("/api/v1/health", async (HttpContext context) =>
                await context.Response.WriteAsJsonAsync(new { status = "ok" }));
            application.MapGet("/api/v1/status", async (HttpContext context) =>
                await context.Response.WriteAsJsonAsync(await handler.GetStatusAsync(context.RequestAborted)));
            application.MapGet("/api/v1/profiles", async (HttpContext context) =>
                await context.Response.WriteAsJsonAsync(await handler.GetProfilesAsync(context.RequestAborted)));
            application.MapPost("/api/v1/profiles/{profileId}/activate", async (HttpContext context, string profileId) =>
            {
                var result = await handler.ActivateProfileAsync(
                    profileId,
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.RequestAborted);
                context.Response.StatusCode = result.StatusCode;
                await context.Response.WriteAsJsonAsync(result.Body);
            });
            application.MapPost("/api/v1/work-mode", async (HttpContext context) =>
            {
                var result = await handler.EnterWorkModeAsync(
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.RequestAborted);
                context.Response.StatusCode = result.StatusCode;
                await context.Response.WriteAsJsonAsync(result.Body);
            });

            await application.StartAsync(cancellationToken);
            _application = application;
            BoundPort = ResolveBoundPort(application, options.Port);
            _logger.LogInformation(
                "[REMOTE CONTROL HOST] Enabled=True Port={Port} PrivateNetworkOnly={PrivateNetworkOnly} Result=Started",
                options.Port,
                options.AllowPrivateNetworkOnly);
        }
        catch (Exception exception)
        {
            if (application is not null)
            {
                await application.DisposeAsync();
            }

            _applicationCancellation.Dispose();
            _applicationCancellation = null;
            _logger.LogError(
                exception,
                "[REMOTE CONTROL HOST] Enabled=True Port={Port} PrivateNetworkOnly={PrivateNetworkOnly} Result=Failed",
                options.Port,
                options.AllowPrivateNetworkOnly);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_application is null)
        {
            return;
        }

        try
        {
            _applicationCancellation?.Cancel();
            await _application.StopAsync(cancellationToken);
            await _application.DisposeAsync();
            _logger.LogInformation("[REMOTE CONTROL HOST] Result=Stopped");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[REMOTE CONTROL HOST] Result=StopFailed");
        }
        finally
        {
            _application = null;
            BoundPort = null;
            _applicationCancellation?.Dispose();
            _applicationCancellation = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private static int ResolveBoundPort(WebApplication application, int configuredPort)
    {
        if (configuredPort != 0)
        {
            return configuredPort;
        }

        var addresses = application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;
        var address = addresses?.FirstOrDefault();
        return address is not null && Uri.TryCreate(address, UriKind.Absolute, out var uri)
            ? uri.Port
            : 0;
    }
}
