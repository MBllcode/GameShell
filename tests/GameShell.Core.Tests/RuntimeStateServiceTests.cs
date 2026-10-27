using GameShell.Core.Models;
using GameShell.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameShell.Core.Tests;

public sealed class RuntimeStateServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "GameShell-runtime-" + Guid.NewGuid());
    private RuntimeStateService Service => new(NullLogger<RuntimeStateService>.Instance, Path.Combine(_directory, "state.json"));

    [Fact]
    public async Task ConcurrentReadersAndWriters_NeverObserveTruncatedState()
    {
        var service = Service;
        await service.SaveAsync(Profile());
        await Task.WhenAll(Enumerable.Range(0, 80).Select(async i =>
        {
            if (i % 2 == 0) await service.SaveAsync(Profile());
            else Assert.Equal("TV", (await service.LoadAsync()).LastActiveProfileId);
        }));
    }

    [Fact]
    public async Task CancelledSave_PreservesCommittedState()
    {
        var service = Service;
        await service.SaveAsync(Profile());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveAsync(new RuntimeState(), cancellation.Token));
        Assert.Equal("TV", (await Service.LoadAsync()).LastActiveProfileId);
    }

    [Fact]
    public async Task CorruptPrimary_UsesLastCompleteBackup()
    {
        var service = Service;
        await service.SaveAsync(Profile());
        await service.SaveAsync(Profile());
        await File.WriteAllTextAsync(service.StatePath, "{truncated");
        Assert.Equal("TV", (await Service.LoadAsync()).LastActiveProfileId);
    }

    [Fact]
    public async Task LegacyRuntimeWithoutLastActiveFields_InfersProfileMode()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Service.StatePath, """{"currentMode":"PcGaming"}""");
        Assert.Equal("Profile", (await Service.LoadAsync()).LastActiveMode);
    }

    [Fact]
    public async Task FailedTransition_DoesNotReplaceLastCommittedTarget()
    {
        var service = Service;
        var state = Profile();
        state.CurrentMode = GameShellMode.Work;
        state.LastResult = "Failed";
        await service.SaveAsync(state);
        Assert.Equal("Profile", (await Service.LoadAsync()).LastActiveMode);
        Assert.Equal("TV", (await Service.LoadAsync()).LastActiveProfileId);
    }

    private static RuntimeState Profile() => new()
    {
        CurrentMode = GameShellMode.GamingProfile, LastActiveMode = "Profile", LastActiveProfileId = "TV"
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
