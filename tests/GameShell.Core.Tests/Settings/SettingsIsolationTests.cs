using GameShell.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class SettingsIsolationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "GameShell-settings-" + Guid.NewGuid());
    private string SettingsPath => Path.Combine(_directory, "settings.json");
    private SettingsService Service => new(NullLogger<SettingsService>.Instance, SettingsPath);

    [Fact]
    public async Task UnsavedMutations_DoNotAffectOtherReadersOrNestedCollections()
    {
        var service = Service;
        await service.SaveAsync(new AppSettings { Startup = { StartWithWindows = true } });
        var first = await service.LoadAsync();
        var other = await service.LoadAsync();
        first.Startup.StartWithWindows = false;
        first.Gaming.ProtectedProcessNames.Clear();
        first.Display.GamingProfiles.Add(new DisplayLayoutProfile { Name = "UNSAVED" });
        Assert.True(other.Startup.StartWithWindows);
        var loaded = await service.LoadAsync();
        Assert.True(loaded.Startup.StartWithWindows);
        Assert.NotEmpty(loaded.Gaming.ProtectedProcessNames);
        Assert.Empty(loaded.Display.GamingProfiles);
    }

    [Fact]
    public async Task SuccessfulSave_IsVisibleButCallerCannotMutateSavedSnapshot()
    {
        var service = Service;
        var input = new AppSettings { Startup = { StartWithWindows = true } };
        await service.SaveAsync(input);
        input.Startup.StartWithWindows = false;
        Assert.True((await service.LoadAsync()).Startup.StartWithWindows);
        await service.SaveAsync(input);
        Assert.False((await service.LoadAsync()).Startup.StartWithWindows);
        Assert.False((await Service.LoadAsync()).Startup.StartWithWindows);
    }

    [Fact]
    public async Task FailedSave_PreservesPreviousCacheAndFile()
    {
        var service = Service;
        await service.SaveAsync(new AppSettings { Startup = { StartWithWindows = true } });
        var before = await File.ReadAllTextAsync(SettingsPath);
        var changed = await service.LoadAsync();
        changed.Startup.StartWithWindows = false;
        await using (var locked = new FileStream(SettingsPath + ".tmp", FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => service.SaveAsync(changed));
        }
        Assert.True((await service.LoadAsync()).Startup.StartWithWindows);
        Assert.Equal(before, await File.ReadAllTextAsync(SettingsPath));
    }

    [Fact]
    public async Task ReadAccessFailure_IsPropagatedWithoutRecoveryOrOverwriting()
    {
        await Service.SaveAsync(new AppSettings());
        var before = await File.ReadAllTextAsync(SettingsPath);
        await using (var locked = new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => Service.LoadAsync());
        }
        Assert.Equal(before, await File.ReadAllTextAsync(SettingsPath));
        Assert.Empty(Directory.GetFiles(_directory, "*.corrupt-*"));
    }

    [Fact]
    public async Task CancelledLoad_DoesNotCreateDefaultsOrRecoveryFiles()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.LoadAsync(cancellation.Token));
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public async Task AccessDenied_IsPropagatedWithoutReplacingConfiguration()
    {
        Directory.CreateDirectory(SettingsPath);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.LoadAsync());
        Assert.True(Directory.Exists(SettingsPath));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
