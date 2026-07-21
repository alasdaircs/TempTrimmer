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

    public IReadOnlySet<string> EnvOverridden { get; private set; } = new HashSet<string>();

    public bool IsEnvOverridden(string optionName) => EnvOverridden.Contains(optionName);

    public void OnGet()
    {
        EnvOverridden = DetectEnvOverrides();

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
        EnvOverridden = DetectEnvOverrides();

        if (!ModelState.IsValid) return Page();

        // Env-overridden fields are read-only in the UI; persist the current effective value
        // rather than whatever came back in the form.
        var current = _options.Value;
        var updated = new TrimmerOptions
        {
            MaxAge = IsEnvOverridden(nameof(TrimmerOptions.MaxAge))
                ? current.MaxAge
                : TimeSpan.FromDays(Form.MaxAgeDays) + TimeSpan.FromHours(Form.MaxAgeHours),
            MaxTotalSizeMb = IsEnvOverridden(nameof(TrimmerOptions.MaxTotalSizeMb))
                ? current.MaxTotalSizeMb
                : Form.MaxTotalSizeMb,
            ScanInterval = IsEnvOverridden(nameof(TrimmerOptions.ScanInterval))
                ? current.ScanInterval
                : TimeSpan.FromMinutes(Form.ScanIntervalMinutes),
            ApiKey = IsEnvOverridden(nameof(TrimmerOptions.ApiKey))
                ? current.ApiKey
                : Form.ApiKey?.Trim() ?? string.Empty,
            TempPath = current.TempPath,
            ExcludedFolders = IsEnvOverridden(nameof(TrimmerOptions.ExcludedFolders))
                ? current.ExcludedFolders
                : ParseLines(Form.ExcludedFolders),
            ExcludedFiles = IsEnvOverridden(nameof(TrimmerOptions.ExcludedFiles))
                ? current.ExcludedFiles
                : ParseLines(Form.ExcludedFiles),
            DryRun = IsEnvOverridden(nameof(TrimmerOptions.DryRun))
                ? current.DryRun
                : Form.DryRun,
        };

        await _persistence.SaveOptionsAsync(updated);
        TempData["Message"] = "Configuration saved successfully.";
        return RedirectToPage();
    }

    private static readonly string[] OptionNames =
    [
        nameof(TrimmerOptions.MaxAge),
        nameof(TrimmerOptions.MaxTotalSizeMb),
        nameof(TrimmerOptions.ScanInterval),
        nameof(TrimmerOptions.ApiKey),
        nameof(TrimmerOptions.ExcludedFolders),
        nameof(TrimmerOptions.ExcludedFiles),
        nameof(TrimmerOptions.DryRun),
    ];

    private static HashSet<string> DetectEnvOverrides()
    {
        var envKeys = Environment.GetEnvironmentVariables().Keys
            .Cast<string>()
            .ToArray();

        var overridden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in OptionNames)
        {
            var flat = $"{TrimmerOptions.Section}__{name}";
            var nested = $"{TrimmerOptions.Section}:{name}";
            if (envKeys.Any(k =>
                    k.Equals(flat, StringComparison.OrdinalIgnoreCase)
                    || k.StartsWith(flat + "__", StringComparison.OrdinalIgnoreCase)
                    || k.Equals(nested, StringComparison.OrdinalIgnoreCase)
                    || k.StartsWith(nested + ":", StringComparison.OrdinalIgnoreCase)))
                overridden.Add(name);
        }
        return overridden;
    }

    private static string[] ParseLines(string? value) =>
        value?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             ?? [];
}

public sealed class ConfigFormModel
{
    [Range(0, 365)] public int MaxAgeDays { get; set; }
    [Range(0, 23)] public int MaxAgeHours { get; set; }
    [Range(1, 102400)] public long MaxTotalSizeMb { get; set; } = 256;
    [Range(1, 1440)] public int ScanIntervalMinutes { get; set; } = 15;
    public string? ApiKey { get; set; }
    public string ExcludedFolders { get; set; } = "/jobs*";
    public string ExcludedFiles { get; set; } = "/applicationhost.config";
    public bool DryRun { get; set; } = true;
}
