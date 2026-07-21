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
        _settingsPath = GetSettingsFilePath(env.ContentRootPath);
        _logger = logger;
    }

    /// <summary>
    /// UI-saved settings must live outside the extension folder, which is wiped on every
    /// upgrade or ARM re-install. On App Service %HOME% is the durable shared content root;
    /// locally (no HOME) fall back to the content root.
    /// </summary>
    public static string GetSettingsFilePath(string contentRootPath)
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        var baseDir = string.IsNullOrWhiteSpace(home) ? contentRootPath : Path.Combine(home, "data");
        return Path.Combine(baseDir, "TempTrimmer", "settings.json");
    }

    public async Task SaveOptionsAsync(TrimmerOptions options, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

            var root = new Dictionary<string, object>
            {
                [TrimmerOptions.Section] = new
                {
                    MaxAge = options.MaxAge.ToString(),
                    options.MaxTotalSizeMb,
                    options.TempPath,
                    options.ApiKey,
                    ScanInterval = options.ScanInterval.ToString(),
                    options.ExcludedFolders,
                    options.ExcludedFiles,
                    options.DryRun,
                },
            };

            var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json, ct);

            _configRoot.Reload();
            _logger.LogInformation("Configuration saved to {Path} and reloaded.", _settingsPath);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
