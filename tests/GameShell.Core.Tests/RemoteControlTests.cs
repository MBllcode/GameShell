using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GameShell.Core.Interfaces;
using GameShell.Core.Models;
using GameShell.Desktop.Remote;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class RemoteControlTests
{
    [Fact]
    public async Task Disabled_remote_control_does_not_start_host()
    {
        await using var host = new RemoteControlHost(
            new FakeSettingsService(new AppSettings()),
            new FakeModeCommandService(),
            NullLogger<RemoteControlHost>.Instance,
            NullLoggerFactory.Instance);

        await host.StartAsync(new AppSettings());

        Assert.False(host.IsRunning);
        Assert.Null(host.BoundPort);
    }

    [Fact]
    public void Access_requires_bearer_token_for_protected_endpoint()
    {
        var context = CreateContext("/api/v1/profiles", IPAddress.Loopback);

        var result = RemoteControlAuthentication.CheckRequest(
            context,
            new RemoteControlOptions(true, 47777, "secret", true));

        Assert.False(result.Allowed);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Equal("Unauthorized", result.Error);
    }

    [Fact]
    public void Access_accepts_correct_bearer_token_and_rejects_wrong_token()
    {
        var wrongContext = CreateContext("/api/v1/status", IPAddress.Loopback);
        wrongContext.Request.Headers.Authorization = "Bearer wrong";
        var correctContext = CreateContext("/api/v1/status", IPAddress.Loopback);
        correctContext.Request.Headers.Authorization = "Bearer secret";
        var options = new RemoteControlOptions(true, 47777, "secret", true);

        var wrong = RemoteControlAuthentication.CheckRequest(wrongContext, options);
        var correct = RemoteControlAuthentication.CheckRequest(correctContext, options);

        Assert.False(wrong.Allowed);
        Assert.Equal(StatusCodes.Status401Unauthorized, wrong.StatusCode);
        Assert.True(correct.Allowed);
    }

    [Fact]
    public void Health_is_public_but_still_private_network_only()
    {
        var local = RemoteControlAuthentication.CheckRequest(
            CreateContext("/api/v1/health", IPAddress.Loopback),
            new RemoteControlOptions(true, 47777, "secret", true));
        var publicAddress = RemoteControlAuthentication.CheckRequest(
            CreateContext("/api/v1/health", IPAddress.Parse("8.8.8.8")),
            new RemoteControlOptions(true, 47777, "secret", true));

        Assert.True(local.Allowed);
        Assert.False(publicAddress.Allowed);
        Assert.Equal(StatusCodes.Status403Forbidden, publicAddress.StatusCode);
    }

    [Theory]
    [InlineData("10.20.30.40")]
    [InlineData("172.16.4.5")]
    [InlineData("192.168.50.10")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("fc00::10")]
    [InlineData("fe80::10")]
    public void Private_network_filter_accepts_local_and_private_addresses(string address)
    {
        Assert.True(RemoteControlAuthentication.IsPrivateOrLocalAddress(IPAddress.Parse(address)));
    }

    [Fact]
    public async Task Profiles_returns_configured_profiles_only()
    {
        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                GamingProfiles =
                [
                    new DisplayLayoutProfile { Name = "CONSOLE" },
                    new DisplayLayoutProfile { Name = "PC" }
                ]
            }
        };
        var mode = new FakeModeCommandService();
        var handler = CreateHandler(settings, mode);

        var profiles = await handler.GetProfilesAsync(CancellationToken.None);

        Assert.Equal(["CONSOLE", "PC"], profiles.Select(profile => profile.Id).ToArray());
        Assert.All(profiles, profile => Assert.Equal(profile.Id, profile.Name));
    }

    [Fact]
    public async Task Unknown_profile_returns_not_found_without_dispatching()
    {
        var mode = new FakeModeCommandService();
        var handler = CreateHandler(new AppSettings(), mode);

        var result = await handler.ActivateProfileAsync("UNKNOWN", "127.0.0.1", CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("ProfileNotFound", result.Body.Error);
        Assert.Equal(0, mode.ProfileCalls);
    }

    [Fact]
    public async Task Valid_profile_is_dispatched_once_as_accepted()
    {
        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                GamingProfiles = [new DisplayLayoutProfile { Name = "CONSOLE" }]
            }
        };
        var mode = new FakeModeCommandService();
        var handler = CreateHandler(settings, mode);

        var result = await handler.ActivateProfileAsync("console", "192.168.1.20", CancellationToken.None);

        Assert.Equal(StatusCodes.Status202Accepted, result.StatusCode);
        Assert.True(result.Body.Accepted);
        Assert.Equal("CONSOLE", result.Body.ProfileId);
        Assert.Equal(1, mode.ProfileCalls);
        Assert.Equal("CONSOLE", mode.LastProfile);
    }

    [Fact]
    public async Task Work_mode_is_dispatched_once_as_accepted()
    {
        var mode = new FakeModeCommandService();
        var handler = CreateHandler(new AppSettings(), mode);

        var result = await handler.EnterWorkModeAsync("192.168.1.20", CancellationToken.None);

        Assert.Equal(StatusCodes.Status202Accepted, result.StatusCode);
        Assert.True(result.Body.Accepted);
        Assert.Equal(1, mode.WorkModeCalls);
    }

    [Fact]
    public async Task Busy_status_returns_conflict_without_dispatching_another_transition()
    {
        var settings = new AppSettings
        {
            Display = new DisplaySettings
            {
                GamingProfiles = [new DisplayLayoutProfile { Name = "CONSOLE" }]
            }
        };
        var mode = new FakeModeCommandService { IsBusy = true };
        var handler = CreateHandler(settings, mode);

        var profileResult = await handler.ActivateProfileAsync("CONSOLE", "192.168.1.20", CancellationToken.None);
        var workResult = await handler.EnterWorkModeAsync("192.168.1.20", CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, profileResult.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, workResult.StatusCode);
        Assert.Equal("TransitionAlreadyRunning", profileResult.Body.Error);
        Assert.Equal("TransitionAlreadyRunning", workResult.Body.Error);
        Assert.Equal(0, mode.ProfileCalls);
        Assert.Equal(0, mode.WorkModeCalls);
    }

    [Fact]
    public async Task Legacy_settings_receive_disabled_remote_control_and_persisted_token()
    {
        var path = Path.Combine(Path.GetTempPath(), "GameShell.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{\"ui\":{\"isFirstRunCompleted\":true}}");

        var service = new GameShell.Core.Services.SettingsService(
            NullLogger<GameShell.Core.Services.SettingsService>.Instance,
            path);
        var first = await service.LoadAsync();
        var second = await service.LoadAsync();
        var json = await File.ReadAllTextAsync(path);

        Assert.False(first.RemoteControl.Enabled);
        Assert.Equal(47777, first.RemoteControl.Port);
        Assert.True(first.RemoteControl.AllowPrivateNetworkOnly);
        Assert.Equal(43, first.RemoteControl.AuthenticationToken.Length);
        Assert.Equal(first.RemoteControl.AuthenticationToken, second.RemoteControl.AuthenticationToken);
        Assert.Contains("remoteControl", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_exposes_authenticated_http_api_and_delegates_profile_command()
    {
        var settings = new AppSettings
        {
            RemoteControl = new RemoteControlSettings
            {
                Enabled = true,
                Port = 0,
                AuthenticationToken = "integration-secret",
                AllowPrivateNetworkOnly = true
            },
            Display = new DisplaySettings
            {
                GamingProfiles = [new DisplayLayoutProfile { Name = "CONSOLE" }]
            }
        };
        var mode = new FakeModeCommandService();
        var settingsService = new FakeSettingsService(settings);
        await using var host = new RemoteControlHost(
            settingsService,
            mode,
            NullLogger<RemoteControlHost>.Instance,
            NullLoggerFactory.Instance);

        await host.StartAsync(settings);
        Assert.True(host.BoundPort is > 0, $"Host did not expose a bound port: {host.BoundPort}");
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{host.BoundPort}") };

        var health = await client.GetAsync("/api/v1/health");
        var unauthorizedProfiles = await client.GetAsync("/api/v1/profiles");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "integration-secret");
        var profilesResponse = await client.GetAsync("/api/v1/profiles");
        var profilesBody = await profilesResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, profilesResponse.StatusCode);
        Assert.False(
            string.IsNullOrWhiteSpace(profilesBody),
            $"Empty profiles response. Status={profilesResponse.StatusCode} Uri={profilesResponse.RequestMessage?.RequestUri} ContentType={profilesResponse.Content.Headers.ContentType}");
        var profiles = JsonSerializer.Deserialize<RemoteProfileResponse[]>(profilesBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var activation = await client.PostAsync("/api/v1/profiles/CONSOLE/activate", content: null);

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedProfiles.StatusCode);
        Assert.Equal(["CONSOLE"], profiles!.Select(profile => profile.Id).ToArray());
        Assert.Equal(HttpStatusCode.Accepted, activation.StatusCode);
        Assert.Equal(1, mode.ProfileCalls);
        Assert.Equal("CONSOLE", mode.LastProfile);
    }

    private static DefaultHttpContext CreateContext(string path, IPAddress address)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = address;
        return context;
    }

    private static RemoteControlCommandHandler CreateHandler(AppSettings settings, FakeModeCommandService mode)
    {
        var settingsService = new FakeSettingsService(settings);
        return new RemoteControlCommandHandler(
            settingsService,
            mode,
            NullLogger<RemoteControlCommandHandler>.Instance,
            schedule: work => work());
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly AppSettings _settings;

        public FakeSettingsService(AppSettings settings)
        {
            _settings = settings;
        }

        public string SettingsPath => "fake-settings.json";

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_settings);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeModeCommandService : IModeCommandService
    {
        public bool IsBusy { get; init; }
        public int ProfileCalls { get; private set; }
        public int WorkModeCalls { get; private set; }
        public string? LastProfile { get; private set; }

        public Task<AppStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AppStatus
            {
                IsBusy = IsBusy,
                CurrentMode = GameShellMode.Work.ToString(),
                SessionState = "Idle"
            });
        }

        public Task<ModeResult> EnterGameShellAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SuccessResult());
        }

        public Task<ModeResult> EnterGameShellWithDisplayProfileAsync(string profileName, CancellationToken cancellationToken = default)
        {
            ProfileCalls++;
            LastProfile = profileName;
            return Task.FromResult(SuccessResult());
        }

        public Task<ModeResult> EnterWorkModeAsync(CancellationToken cancellationToken = default)
        {
            WorkModeCalls++;
            return Task.FromResult(SuccessResult());
        }

        private static ModeResult SuccessResult() => new()
        {
            Status = ModeResultStatus.Success,
            CurrentState = ModeState.GameShell
        };
    }
}
