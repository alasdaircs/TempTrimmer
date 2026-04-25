using AcsSolutions.TempTrimmer.Models;
using AcsSolutions.TempTrimmer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AcsSolutions.TempTrimmer.Pages;

public class IndexModel : PageModel
{
    private readonly TrimEngine _engine;
    private readonly TrimState _state;
    private readonly DeletionLogService _deletionLog;
    private readonly IOptionsMonitor<TrimmerOptions> _options;

    public IndexModel(
        TrimEngine engine,
        TrimState state,
        DeletionLogService deletionLog,
        IOptionsMonitor<TrimmerOptions> options)
    {
        _engine = engine;
        _state = state;
        _deletionLog = deletionLog;
        _options = options;
    }

    public TempFolderStats? Stats { get; private set; }
    public TrimResult? LastResult { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsDryRun { get; private set; }
    public long FreeSpaceBytes { get; private set; } = -1;
    public string? FreeDriveName { get; private set; }
    public IReadOnlyList<DeletedFileInfo>? DryRunPreview { get; private set; }
    public IReadOnlyList<DeletionLogEntry>? DeletionLog { get; private set; }

    public async Task OnGetAsync()
    {
        var opts = _options.CurrentValue;
        Stats = _engine.GetStats(opts.TempPath);
        LastResult = _state.LastResult;
        IsRunning = _state.IsRunning;
        IsDryRun = opts.DryRun;

        var expandedPath = Environment.ExpandEnvironmentVariables(opts.TempPath);
        try
        {
            var root = Path.GetPathRoot(expandedPath);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                FreeSpaceBytes = drive.AvailableFreeSpace;
                FreeDriveName = drive.Name;
            }
        }
        catch { }

        if (IsDryRun)
            DryRunPreview = _engine.GetCandidates(opts);
        else
            DeletionLog = await _deletionLog.GetEntriesAsync();
    }

    public async Task<IActionResult> OnPostRunNowAsync()
    {
        if (!_state.TrySetRunning())
        {
            TempData["Message"] = "A trim run is already in progress.";
            return RedirectToPage();
        }

        var opts = _options.CurrentValue;
        TrimResult? result = null;
        try
        {
            result = _engine.Execute(opts);

            if (!result.IsDryRun && result.DeletedFiles.Count > 0)
                await _deletionLog.AppendAsync(result.DeletedFiles, result.CompletedAt);

            TempData["Message"] = result.IsDryRun
                ? $"Dry run complete — {result.DeletedFiles.Count} file(s) would be deleted ({result.TotalBytesFreed / 1_048_576.0:F1} MB)."
                : $"Trim complete — {result.DeletedFiles.Count} file(s) deleted, {result.TotalBytesFreed / 1_048_576.0:F1} MB freed.";
        }
        catch (Exception)
        {
            result = new TrimResult
            {
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow,
            };
            throw;
        }
        finally
        {
            _state.SetCompleted(result!);
        }

        return RedirectToPage();
    }
}
