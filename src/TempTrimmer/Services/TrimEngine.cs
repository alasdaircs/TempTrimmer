using AcsSolutions.TempTrimmer.Models;

namespace AcsSolutions.TempTrimmer.Services;

public sealed class TrimEngine
{
    private readonly ILogger<TrimEngine> _logger;

    public TrimEngine(ILogger<TrimEngine> logger) => _logger = logger;

    public TrimResult Execute(TrimmerOptions options)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var expandedPath = Environment.ExpandEnvironmentVariables(options.TempPath);
        var maxBytes = options.MaxTotalSizeMb * 1024L * 1024L;
        var cutoffUtc = startedAt - options.MaxAge;

        _logger.LogInformation(
            "Trim run started. Path={Path} MaxAge={MaxAge} MaxTotalSizeMb={MaxTotalSizeMb}",
            expandedPath, options.MaxAge, options.MaxTotalSizeMb);

        var allFiles = EnumerateFiles(expandedPath);

        // Condition 1 — age: mark files whose last-write time predates the cutoff.
        var condemned = new Dictionary<string, (FileInfo File, DeletionReason Reason)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var f in allFiles)
        {
            if (new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero) < cutoffUtc)
                condemned[f.FullName] = (f, DeletionReason.TooOld);
        }

        // Condition 2 — quota: if the total size of ALL files exceeds the threshold,
        // mark the oldest surviving files until the projected total would be within quota.
        var totalBytes = allFiles.Sum(f => f.Length);
        if (totalBytes > maxBytes)
        {
            foreach (var f in allFiles.OrderBy(f => f.LastWriteTimeUtc))
            {
                if (totalBytes <= maxBytes) break;
                if (!condemned.ContainsKey(f.FullName))
                    condemned[f.FullName] = (f, DeletionReason.OverQuota);
                totalBytes -= f.Length;
            }
        }

        var deleted = new List<DeletedFileInfo>();
        var errors = new List<string>();

        foreach (var (path, (file, reason)) in condemned)
        {
            try
            {
                File.Delete(path);
                deleted.Add(new DeletedFileInfo(
                    path,
                    file.Length,
                    new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
                    reason));

                _logger.LogDebug("Deleted {Path} reason={Reason} size={SizeBytes}",
                    path, reason, file.Length);
            }
            catch (Exception ex)
            {
                errors.Add($"{path}: {ex.Message}");
                _logger.LogWarning("Could not delete {Path}: {Error}", path, ex.Message);
            }
        }

        DeleteEmptyDirectories(expandedPath);

        var result = new TrimResult
        {
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            DeletedFiles = deleted,
            Errors = errors,
        };

        _logger.LogInformation(
            "Trim run complete. Deleted={Count} BytesFreed={BytesFreed} Errors={ErrorCount} Duration={DurationMs}ms",
            deleted.Count, result.TotalBytesFreed, errors.Count,
            (long)result.Duration.TotalMilliseconds);

        return result;
    }

    public TempFolderStats GetStats(string tempPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(tempPath);
        var files = EnumerateFiles(expanded);
        if (files.Count == 0)
            return new TempFolderStats(expanded, 0, 0, null, null);

        return new TempFolderStats(
            expanded,
            files.Sum(f => f.Length),
            files.Count,
            new DateTimeOffset(files.Min(f => f.LastWriteTimeUtc), TimeSpan.Zero),
            new DateTimeOffset(files.Max(f => f.LastWriteTimeUtc), TimeSpan.Zero));
    }

    private static IReadOnlyList<FileInfo> EnumerateFiles(string root)
    {
        var result = new List<FileInfo>();
        if (!Directory.Exists(root)) return result;

        var queue = new Queue<string>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var dir = queue.Dequeue();
            try
            {
                foreach (var file in Directory.GetFiles(dir))
                    result.Add(new FileInfo(file));
                foreach (var sub in Directory.GetDirectories(dir))
                    queue.Enqueue(sub);
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
        }

        return result;
    }

    private static void DeleteEmptyDirectories(string root)
    {
        // Process deepest paths first so parent directories become empty after children are removed.
        foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                                     .OrderByDescending(d => d.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch { }
        }
    }
}
