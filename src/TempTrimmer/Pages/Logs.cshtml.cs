using AcsSolutions.TempTrimmer.Models;
using AcsSolutions.TempTrimmer.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcsSolutions.TempTrimmer.Pages;

public class LogsModel : PageModel
{
    private readonly LogReaderService _logReader;

    public LogsModel(LogReaderService logReader) => _logReader = logReader;

    public IReadOnlyList<LogEntry> Entries { get; private set; } = [];
    public string SelectedLevel { get; private set; } = "All";
    public int PageSize { get; private set; } = 100;
    public string LogDirectory { get; private set; } = string.Empty;

    public void OnGet(string? level = null, int lines = 100)
    {
        SelectedLevel = string.IsNullOrWhiteSpace(level) ? "All" : level;
        PageSize = lines is > 0 and <= 500 ? lines : 100;
        LogDirectory = _logReader.GetLogDirectory();

        Entries = _logReader.ReadRecent(
            PageSize,
            SelectedLevel == "All" ? null : SelectedLevel);
    }
}
