using System.Text.Json;
using AcsSolutions.TempTrimmer.Models;

namespace AcsSolutions.TempTrimmer.Services;

public sealed class ConfigPersistenceService
{
    private readonly IConfigurationRoot _configRoot;
    private readonly string _settingsPath;
    private readonly ILogger<ConfigPersistenceService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ConfigPersistenceService(
        IConfigurationRoot configRoot,
        IWebHostEnvironment env,
        ILogger<ConfigPersistenceService> logger)
    {
        _configRoot = configRoot;
        _settingsPath = Path.Combine(env.ContentRootPath, "appsettings.json");
        _logger = logger;
    }

    public async Task SaveOptionsAsync(TrimmerOptions options, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath, ct);
            var root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                       ?? throw new InvalidOperationException("Could not parse appsettings.json.");

            root[TrimmerOptions.Section] = JsonSerializer.SerializeToElement(new
            {
                MaxAge = options.MaxAge.ToString(),
                options.MaxTotalSizeMb,
                options.TempPath,
                options.ApiKey,
                ScanInterval = options.ScanInterval.ToString(),
                options.ExcludedFolders,
                options.ExcludedFiles,
                options.DryRun,
            });

            var updated = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, updated, ct);

            _configRoot.Reload();
            _logger.LogInformation("Configuration saved and reloaded.");
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
