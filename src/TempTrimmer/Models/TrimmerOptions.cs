namespace AcsSolutions.TempTrimmer.Models;

public sealed class TrimmerOptions
{
    public const string Section = "TempTrimmer";

    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(72);
    public long MaxTotalSizeMb { get; set; } = 1024;
    public string TempPath { get; set; } = "%TEMP%";
    public string ApiKey { get; set; } = string.Empty;
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(15);
    public string[] ExcludedFolders { get; set; } = ["/jobs*"];
    public string[] ExcludedFiles { get; set; } = ["/applicationhost.config"];
    public bool DryRun { get; set; } = true;
}
