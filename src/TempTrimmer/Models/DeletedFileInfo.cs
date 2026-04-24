namespace AcsSolutions.TempTrimmer.Models;

public sealed record DeletedFileInfo(
    string Path,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc,
    DeletionReason Reason);
