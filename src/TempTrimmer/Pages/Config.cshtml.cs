using System.ComponentModel.DataAnnotations;
using AcsSolutions.TempTrimmer.Models;
using AcsSolutions.TempTrimmer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AcsSolutions.TempTrimmer.Pages;

public class ConfigModel : PageModel
{
    private readonly ConfigPersistenceService _persistence;
    private readonly IOptionsSnapshot<TrimmerOptions> _options;

    public ConfigModel(ConfigPersistenceService persistence, IOptionsSnapshot<TrimmerOptions> options)
    {
        _persistence = persistence;
        _options = options;
    }

    [BindProperty]
    public ConfigFormModel Form { get; set; } = new();

    public void OnGet()
    {
        var opts = _options.Value;
        Form = new ConfigFormModel
        {
            MaxAgeDays = (int)opts.MaxAge.TotalDays,
            MaxAgeHours = opts.MaxAge.Hours,
            MaxTotalSizeMb = opts.MaxTotalSizeMb,
            ScanIntervalMinutes = (int)opts.ScanInterval.TotalMinutes,
            ApiKey = opts.ApiKey,
            ExcludedFolders = string.Join("\n", opts.ExcludedFolders),
            ExcludedFiles = string.Join("\n", opts.ExcludedFiles),
            DryRun = opts.DryRun,
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var current = _options.Value;
        var updated = new TrimmerOptions
        {
            MaxAge = TimeSpan.FromDays(Form.MaxAgeDays) + TimeSpan.FromHours(Form.MaxAgeHours),
            MaxTotalSizeMb = Form.MaxTotalSizeMb,
            ScanInterval = TimeSpan.FromMinutes(Form.ScanIntervalMinutes),
            ApiKey = Form.ApiKey?.Trim() ?? string.Empty,
            TempPath = current.TempPath,
            ExcludedFolders = ParseLines(Form.ExcludedFolders),
            ExcludedFiles = ParseLines(Form.ExcludedFiles),
            DryRun = Form.DryRun,
        };

        await _persistence.SaveOptionsAsync(updated);
        TempData["Message"] = "Configuration saved successfully.";
        return RedirectToPage();
    }

    private static string[] ParseLines(string? value) =>
        value?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             ?? [];
}

public sealed class ConfigFormModel
{
    [Range(0, 365)] public int MaxAgeDays { get; set; }
    [Range(0, 23)] public int MaxAgeHours { get; set; }
    [Range(1, 102400)] public long MaxTotalSizeMb { get; set; } = 1024;
    [Range(1, 1440)] public int ScanIntervalMinutes { get; set; } = 15;
    public string? ApiKey { get; set; }
    public string ExcludedFolders { get; set; } = "/jobs*";
    public string ExcludedFiles { get; set; } = "/applicationhost.config";
    public bool DryRun { get; set; } = true;
}
