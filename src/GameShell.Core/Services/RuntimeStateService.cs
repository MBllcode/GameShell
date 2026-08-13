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
        if (!File.Exists(StatePath))
        {
            return new RuntimeState();
        }

        try
        {
            var json = await File.ReadAllTextAsync(StatePath, cancellationToken);
            // Keep state.json readable after the runtime enum was renamed from PcGaming to GamingProfile.
            json = json.Replace("\"PcGaming\"", "\"GamingProfile\"", StringComparison.Ordinal);
            return JsonSerializer.Deserialize<RuntimeState>(json, JsonOptions)
                ?? new RuntimeState();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "Runtime state could not be loaded. A new runtime state will be used.");
            return new RuntimeState();
        }
    }

    public async Task SaveAsync(RuntimeState state, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        await using var stream = File.Create(StatePath);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
    }
}
