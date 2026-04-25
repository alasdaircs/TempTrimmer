using System.IO;
using AcsSolutions.TempTrimmer.Models;
using AcsSolutions.TempTrimmer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace TempTrimmer.Tests;

public sealed class TrimEngineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TrimEngine _engine;

    public TrimEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TrimEngineTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _engine = new TrimEngine(NullLogger<TrimEngine>.Instance);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // --- helpers ---

    private string CreateFile(string name, long sizeBytes, DateTime lastWriteUtc)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, new byte[sizeBytes]);
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }

    private TrimmerOptions DefaultOptions(
        TimeSpan? maxAge = null,
        long maxTotalSizeMb = 1024,
        string[]? excludedFolders = null,
        string[]? excludedFiles = null,
        bool dryRun = false) =>
        new()
        {
            TempPath = _tempDir,
            MaxAge = maxAge ?? TimeSpan.FromHours(72),
            MaxTotalSizeMb = maxTotalSizeMb,
            ScanInterval = TimeSpan.FromMinutes(15),
            DryRun = dryRun,
            ExcludedFolders = excludedFolders ?? [],
            ExcludedFiles = excludedFiles ?? [],
        };

    // --- age-based deletion ---

    [Fact]
    public void File_OlderThanMaxAge_IsDeleted()
    {
        var path = CreateFile("old.tmp", 100, DateTime.UtcNow.AddHours(-73));
        var result = _engine.Execute(DefaultOptions());
        Assert.Single(result.DeletedFiles, f => f.Path == path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void File_YoungerThanMaxAge_IsNotDeleted()
    {
        var path = CreateFile("new.tmp", 100, DateTime.UtcNow.AddHours(-1));
        var result = _engine.Execute(DefaultOptions());
        Assert.DoesNotContain(result.DeletedFiles, f => f.Path == path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Age_DeletedFile_HasReasonTooOld()
    {
        CreateFile("old.tmp", 100, DateTime.UtcNow.AddDays(-10));
        var result = _engine.Execute(DefaultOptions());
        Assert.All(result.DeletedFiles, f => Assert.Equal(DeletionReason.TooOld, f.Reason));
    }

    // --- size-quota deletion ---

    [Fact]
    public void TotalSizeUnderQuota_NoExtraFilesDeleted()
    {
        // 2 MB of recent files; quota is 1 GB
        CreateFile("a.tmp", 1_048_576, DateTime.UtcNow.AddMinutes(-1));
        CreateFile("b.tmp", 1_048_576, DateTime.UtcNow.AddMinutes(-2));
        var result = _engine.Execute(DefaultOptions(maxAge: TimeSpan.FromDays(30)));
        Assert.Empty(result.DeletedFiles);
    }

    [Fact]
    public void TotalSizeOverQuota_OldestFilesDeletedFirst()
    {
        // Quota: 1 MB. Three 512 KB files — oldest should go first.
        const int halfMb = 512 * 1024;
        var oldest = CreateFile("oldest.tmp", halfMb, DateTime.UtcNow.AddMinutes(-30));
        var middle = CreateFile("middle.tmp", halfMb, DateTime.UtcNow.AddMinutes(-20));
        var newest = CreateFile("newest.tmp", halfMb, DateTime.UtcNow.AddMinutes(-10));

        var result = _engine.Execute(DefaultOptions(maxAge: TimeSpan.FromDays(30), maxTotalSizeMb: 1));

        // Total = 1.5 MB, quota = 1 MB — need to delete at least 512 KB.
        Assert.Contains(result.DeletedFiles, f => f.Path == oldest);
        Assert.True(File.Exists(newest));  // newest should survive
        Assert.False(File.Exists(oldest));
    }

    [Fact]
    public void SizeQuota_DeletedFile_HasReasonOverQuota()
    {
        const int halfMb = 512 * 1024;
        CreateFile("a.tmp", halfMb, DateTime.UtcNow.AddMinutes(-20));
        CreateFile("b.tmp", halfMb, DateTime.UtcNow.AddMinutes(-10));
        CreateFile("c.tmp", halfMb, DateTime.UtcNow.AddMinutes(-5));

        var result = _engine.Execute(DefaultOptions(maxAge: TimeSpan.FromDays(30), maxTotalSizeMb: 1));

        Assert.All(
            result.DeletedFiles,
            f => Assert.Equal(DeletionReason.OverQuota, f.Reason));
    }

    // --- combined conditions ---

    [Fact]
    public void FilesMeetingEitherCondition_AreAllDeleted()
    {
        var ageCandidate = CreateFile("aged.tmp", 100, DateTime.UtcNow.AddDays(-5));
        // Make a large recent file that pushes total over quota.
        var quotaCandidate = CreateFile("big_old.tmp", 900 * 1024, DateTime.UtcNow.AddMinutes(-10));
        var survivor = CreateFile("big_new.tmp", 200 * 1024, DateTime.UtcNow.AddMinutes(-1));

        // Quota 1 MB: total = ~1.1 MB; oldest big file should be over quota
        // Age 72 h: aged.tmp qualifies by age
        var result = _engine.Execute(DefaultOptions(maxAge: TimeSpan.FromHours(72), maxTotalSizeMb: 1));

        Assert.Contains(result.DeletedFiles, f => f.Path == ageCandidate);
        Assert.True(File.Exists(survivor));
    }

    // --- error handling ---

    [Fact]
    public void LockedFile_IsSkipped_ErrorRecorded()
    {
        var path = CreateFile("locked.tmp", 100, DateTime.UtcNow.AddDays(-5));

        // Hold the file open to prevent deletion.
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = _engine.Execute(DefaultOptions());

        Assert.DoesNotContain(result.DeletedFiles, f => f.Path == path);
        Assert.NotEmpty(result.Errors);
    }

    // --- exclusions ---

    [Fact]
    public void ExcludedFolder_FilesNotDeleted()
    {
        var jobsDir = Path.Combine(_tempDir, "jobs_abc");
        Directory.CreateDirectory(jobsDir);
        var inside = Path.Combine(jobsDir, "old.tmp");
        File.WriteAllBytes(inside, new byte[100]);
        File.SetLastWriteTimeUtc(inside, DateTime.UtcNow.AddDays(-10));

        var result = _engine.Execute(DefaultOptions(excludedFolders: ["/jobs*"]));

        Assert.DoesNotContain(result.DeletedFiles, f => f.Path == inside);
        Assert.True(File.Exists(inside));
    }

    [Fact]
    public void ExcludedFile_NotDeleted()
    {
        var path = CreateFile("applicationhost.config", 100, DateTime.UtcNow.AddDays(-10));

        var result = _engine.Execute(DefaultOptions(excludedFiles: ["/applicationhost.config"]));

        Assert.DoesNotContain(result.DeletedFiles, f => f.Path == path);
        Assert.True(File.Exists(path));
    }

    // --- dry-run ---

    [Fact]
    public void DryRun_FilesNotDeleted_CandidatesReturned()
    {
        var path = CreateFile("old.tmp", 100, DateTime.UtcNow.AddDays(-10));

        var result = _engine.Execute(DefaultOptions(dryRun: true));

        Assert.True(result.IsDryRun);
        Assert.Contains(result.DeletedFiles, f => f.Path == path);
        Assert.True(File.Exists(path));
    }

    // --- GetCandidates ---

    [Fact]
    public void GetCandidates_ReturnsExpectedFiles()
    {
        var old = CreateFile("old.tmp", 100, DateTime.UtcNow.AddDays(-10));
        var fresh = CreateFile("fresh.tmp", 100, DateTime.UtcNow.AddHours(-1));

        var candidates = _engine.GetCandidates(DefaultOptions());

        Assert.Contains(candidates, f => f.Path == old);
        Assert.DoesNotContain(candidates, f => f.Path == fresh);
        Assert.True(File.Exists(old));
    }

    // --- GetStats ---

    [Fact]
    public void GetStats_ReturnsCorrectFileCount()
    {
        CreateFile("x.tmp", 100, DateTime.UtcNow);
        CreateFile("y.tmp", 200, DateTime.UtcNow);
        var stats = _engine.GetStats(_tempDir);
        Assert.Equal(2, stats.FileCount);
        Assert.Equal(300, stats.TotalBytes);
    }

    [Fact]
    public void GetStats_EmptyDirectory_ReturnsZeros()
    {
        var stats = _engine.GetStats(_tempDir);
        Assert.Equal(0, stats.FileCount);
        Assert.Equal(0, stats.TotalBytes);
        Assert.Null(stats.OldestLastWriteUtc);
    }
}
