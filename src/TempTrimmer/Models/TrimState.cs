namespace AcsSolutions.TempTrimmer.Models;

public sealed class TrimState
{
    private volatile TrimResult? _lastResult;
    private int _runningFlag;

    public TrimResult? LastResult => _lastResult;
    public bool IsRunning => Volatile.Read(ref _runningFlag) == 1;

    // Returns true if this call successfully acquired the running slot.
    public bool TrySetRunning() =>
        Interlocked.CompareExchange(ref _runningFlag, 1, 0) == 0;

    public void SetCompleted(TrimResult result)
    {
        _lastResult = result;
        Volatile.Write(ref _runningFlag, 0);
    }
}
