using AcsSolutions.TempTrimmer.Models;
using Microsoft.Extensions.FileSystemGlobbing;

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
            "Trim run started (DryRun={DryRun}). Path={Path} MaxAge={MaxAge} MaxTotalSizeMb={MaxTotalSizeMb}",
            options.DryRun, expandedPath, options.MaxAge, options.MaxTotalSizeMb);

        var folderMatcher = BuildMatcher(options.ExcludedFolders);
        var fileMatcher = BuildMatcher(options.ExcludedFiles);
        var allFiles = EnumerateFiles(expandedPath, folderMatcher, fileMatcher);

        var condemned = BuildCondemnedSet(allFiles, cutoffUtc, maxBytes);

        var deleted = new List<DeletedFileInfo>();
        var errors = new List<string>();

        if (options.DryRun)
        {
            deleted.AddRange(condemned.Values.Select(c => ToInfo(c.File, c.Reason)));
            _logger.LogInformation("Dry-run complete. {Count} file(s) would be deleted.", deleted.Count);
        }
        else
        {
            foreach (var (path, (file, reason)) in condemned)
            {
                try
                {
                    File.Delete(path);
                    deleted.Add(ToInfo(file, reason));
                    _logger.LogDebug("Deleted {Path} reason={Reason} size={SizeBytes}", path, reason, file.Length);
                }
                catch (Exception ex)
                {
                    errors.Add($"{path}: {ex.Message}");
                    _logger.LogWarning("Could not delete {Path}: {Error}", path, ex.Message);
                }
            }

            DeleteEmptyDirectories(expandedPath);
        }

        var result = new TrimResult
        {
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            DeletedFiles = deleted,
            Errors = errors,
            IsDryRun = options.DryRun,
        };

        _logger.LogInformation(
            "Trim run complete (DryRun={IsDryRun}). Files={Count} BytesFreed={BytesFreed} Errors={ErrorCount} Duration={DurationMs}ms",
            result.IsDryRun, deleted.Count, result.TotalBytesFreed, errors.Count,
            (long)result.Duration.TotalMilliseconds);

        return result;
    }

    public IReadOnlyList<DeletedFileInfo> GetCandidates(TrimmerOptions options)
    {
        var expandedPath = Environment.ExpandEnvironmentVariables(options.TempPath);
        var maxBytes = options.MaxTotalSizeMb * 1024L * 1024L;
        var cutoffUtc = DateTimeOffset.UtcNow - options.MaxAge;

        var folderMatcher = BuildMatcher(options.ExcludedFolders);
        var fileMatcher = BuildMatcher(options.ExcludedFiles);
        var allFiles = EnumerateFiles(expandedPath, folderMatcher, fileMatcher);

        return BuildCondemnedSet(allFiles, cutoffUtc, maxBytes).Values
            .Select(c => ToInfo(c.File, c.Reason))
            .ToList();
    }

    public TempFolderStats GetStats(string tempPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(tempPath);
        var files = EnumerateFiles(expanded, null, null);
        if (files.Count == 0)
            return new TempFolderStats(expanded, 0, 0, null, null);

        return new TempFolderStats(
            expanded,
            files.Sum(f => f.Length),
            files.Count,
            new DateTimeOffset(files.Min(f => f.LastWriteTimeUtc), TimeSpan.Zero),
            new DateTimeOffset(files.Max(f => f.LastWriteTimeUtc), TimeSpan.Zero));
    }

    private static Dictionary<string, (FileInfo File, DeletionReason Reason)> BuildCondemnedSet(
        IReadOnlyList<FileInfo> allFiles,
        DateTimeOffset cutoffUtc,
        long maxBytes)
    {
        var condemned = new Dictionary<string, (FileInfo File, DeletionReason Reason)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var f in allFiles)
        {
            if (new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero) < cutoffUtc)
                condemned[f.FullName] = (f, DeletionReason.TooOld);
        }

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

        return condemned;
    }

    private static DeletedFileInfo ToInfo(FileInfo file, DeletionReason reason) =>
        new(file.FullName, file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), reason);

    private static Matcher BuildMatcher(string[]? patterns)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        if (patterns is null) return matcher;
        foreach (var p in patterns)
        {
            var trimmed = p.TrimStart('/');
            if (!string.IsNullOrWhiteSpace(trimmed))
                matcher.AddInclude(trimmed);
        }
        return matcher;
    }

    private static IReadOnlyList<FileInfo> EnumerateFiles(string root, Matcher? folderMatcher, Matcher? fileMatcher)
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
                {
                    if (fileMatcher is not null)
                    {
                        var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
                        if (fileMatcher.Match(rel).HasMatches) continue;
                    }
                    result.Add(new FileInfo(file));
                }

                foreach (var sub in Directory.GetDirectories(dir))
                {
                    if (folderMatcher is not null)
                    {
                        var rel = Path.GetRelativePath(root, sub).Replace('\\', '/');
                        if (folderMatcher.Match(rel).HasMatches) continue;
                    }
                    queue.Enqueue(sub);
                }
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
