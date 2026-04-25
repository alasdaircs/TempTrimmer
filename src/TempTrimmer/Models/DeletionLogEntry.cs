namespace AcsSolutions.TempTrimmer.Models;

public sealed record DeletionLogEntry(
    DateTimeOffset DeletedAt,
    string Path,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc,
    DeletionReason Reason);
