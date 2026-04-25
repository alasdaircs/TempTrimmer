namespace AcsSolutions.TempTrimmer.Models;

public sealed class TrimResult
{
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset CompletedAt { get; init; }
    public IReadOnlyList<DeletedFileInfo> DeletedFiles { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public bool IsDryRun { get; init; }

    public long TotalBytesFreed => DeletedFiles.Sum(f => f.SizeBytes);
    public TimeSpan Duration => CompletedAt - StartedAt;
}
