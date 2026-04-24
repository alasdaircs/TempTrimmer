namespace AcsSolutions.TempTrimmer.Models;

public sealed record TempFolderStats(
    string Path,
    long TotalBytes,
    int FileCount,
    DateTimeOffset? OldestLastWriteUtc,
    DateTimeOffset? NewestLastWriteUtc);
