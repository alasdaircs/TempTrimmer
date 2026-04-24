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
    private readonly IOptionsMonitor<TrimmerOptions> _options;

    public IndexModel(TrimEngine engine, TrimState state, IOptionsMonitor<TrimmerOptions> options)
    {
        _engine = engine;
        _state = state;
        _options = options;
    }

    public TempFolderStats? Stats { get; private set; }
    public TrimResult? LastResult { get; private set; }
    public bool IsRunning { get; private set; }

    public void OnGet()
    {
        Stats = _engine.GetStats(_options.CurrentValue.TempPath);
        LastResult = _state.LastResult;
        IsRunning = _state.IsRunning;
    }

    public IActionResult OnPostRunNow()
    {
        if (!_state.TrySetRunning())
        {
            TempData["Message"] = "A trim run is already in progress.";
            return RedirectToPage();
        }

        TrimResult? result = null;
        try
        {
            result = _engine.Execute(_options.CurrentValue);
            TempData["Message"] =
                $"Trim complete — {result.DeletedFiles.Count} file(s) deleted, {result.TotalBytesFreed / 1_048_576.0:F1} MB freed.";
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
