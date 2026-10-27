using System.Text.Json;
using System.Text.Json.Serialization;
using GameShell.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameShell.Core.Services;

public interface IRuntimeStateService
{
    string StatePath { get; }
    Task<RuntimeState> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(RuntimeState state, CancellationToken cancellationToken = default);
}

public sealed class RuntimeStateService : IRuntimeStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger<RuntimeStateService> _logger;
    private readonly SemaphoreSlim _fileGate = new(1, 1);

    public RuntimeStateService(ILogger<RuntimeStateService> logger)
        : this(logger, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameShell", "state.json"))
    {
    }

    public RuntimeStateService(ILogger<RuntimeStateService> logger, string statePath)
    {
        _logger = logger;
        StatePath = statePath;
    }

    public string StatePath { get; }

    public async Task<RuntimeState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            var state = await TryLoadAsync(StatePath, cancellationToken);
            return state ?? await TryLoadAsync(StatePath + ".bak", cancellationToken) ?? new RuntimeState();
        }
        finally
        {
            _fileGate.Release();
        }
    }

    private async Task<RuntimeState?> TryLoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            // Keep state.json readable after the runtime enum was renamed from PcGaming to GamingProfile.
            json = json.Replace("\"PcGaming\"", "\"GamingProfile\"", StringComparison.Ordinal);
            var state = JsonSerializer.Deserialize<RuntimeState>(json, JsonOptions);
            // Older state files did not persist LastActiveMode. Infer it only
            // when absent; failed transitions must not overwrite a committed target.
            using var document = JsonDocument.Parse(json);
            if (state is not null && !document.RootElement.TryGetProperty("lastActiveMode", out _))
            {
                state.LastActiveMode = state.CurrentMode switch
                {
                    GameShellMode.GamingProfile => "Profile",
                    GameShellMode.Console => "Console",
                    _ => "Work"
                };
            }
            return state;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "Runtime state could not be loaded from {StatePath}.", path);
            return null;
        }
    }

    public async Task SaveAsync(RuntimeState state, CancellationToken cancellationToken = default)
    {
        await _fileGate.WaitAsync(cancellationToken);
        var tempPath = StatePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(StatePath))!);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(StatePath))
                File.Replace(tempPath, StatePath, StatePath + ".bak");
            else
                File.Move(tempPath, StatePath);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            finally
            {
                _fileGate.Release();
            }
        }
    }
}
