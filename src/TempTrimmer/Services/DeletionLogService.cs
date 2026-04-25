using System.Text.Json;
using System.Text.Json.Serialization;
using AcsSolutions.TempTrimmer.Models;

namespace AcsSolutions.TempTrimmer.Services;

public sealed class DeletionLogService
{
    private const int MaxEntries = 500;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    private readonly string _logPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<DeletionLogService> _logger;
    private List<DeletionLogEntry>? _cache;

    public DeletionLogService(ILogger<DeletionLogService> logger)
    {
        _logger = logger;
        var logDir = Path.Combine(
            Path.GetFullPath(Environment.ExpandEnvironmentVariables("%TEMP%")),
            "TempTrimmer");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, "deletion-log.json");
    }

    public async Task AppendAsync(IEnumerable<DeletedFileInfo> files, DateTimeOffset deletedAt)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            foreach (var f in files)
                _cache!.Add(new DeletionLogEntry(deletedAt, f.Path, f.SizeBytes, f.LastWriteTimeUtc, f.Reason));

            if (_cache!.Count > MaxEntries)
                _cache = _cache.Skip(_cache.Count - MaxEntries).ToList();

            await PersistAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<DeletionLogEntry>> GetEntriesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            return _cache!.AsReadOnly();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_cache is not null) return;

        if (!File.Exists(_logPath))
        {
            _cache = [];
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_logPath);
            _cache = JsonSerializer.Deserialize<List<DeletionLogEntry>>(json, SerializerOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not load deletion log: {Error}", ex.Message);
            _cache = [];
        }
    }

    private async Task PersistAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cache, SerializerOptions);
            await File.WriteAllTextAsync(_logPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not persist deletion log: {Error}", ex.Message);
        }
    }
}
