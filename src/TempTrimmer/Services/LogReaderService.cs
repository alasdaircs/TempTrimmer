using System.Text.Json;
using System.Text.RegularExpressions;
using AcsSolutions.TempTrimmer.Models;
using Microsoft.Extensions.Options;

namespace AcsSolutions.TempTrimmer.Services;

public sealed partial class LogReaderService
{
    private readonly IOptionsMonitor<TrimmerOptions> _options;

    public LogReaderService(IOptionsMonitor<TrimmerOptions> options) => _options = options;

    public string GetLogDirectory()
    {
        var tempPath = Environment.ExpandEnvironmentVariables(_options.CurrentValue.TempPath);
        return Path.Combine(tempPath, "TempTrimmer");
    }

    public IReadOnlyList<LogEntry> ReadRecent(int maxEntries = 200, string? levelFilter = null)
    {
        var logDir = GetLogDirectory();

        if (!Directory.Exists(logDir)) return [];

        var files = Directory.GetFiles(logDir, "log-*.jsonl")
                             .OrderByDescending(f => f)
                             .ToList();

        var entries = new List<LogEntry>();
        foreach (var file in files)
        {
            if (entries.Count >= maxEntries) break;
            entries.AddRange(ReadFile(file, maxEntries - entries.Count, levelFilter));
        }

        return [.. entries.OrderByDescending(e => e.Timestamp)];
    }

    private static IEnumerable<LogEntry> ReadFile(string path, int limit, string? levelFilter)
    {
        try
        {
            string[] lines;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, System.Text.Encoding.UTF8))
                lines = reader.ReadToEnd().Split('\n');
            var result = new List<LogEntry>();
            for (var i = lines.Length - 1; i >= 0 && result.Count < limit; i--)
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = ParseLine(line);
                    if (levelFilter is null ||
                        entry.Level.Equals(levelFilter, StringComparison.OrdinalIgnoreCase))
                        result.Add(entry);
                }
                catch { }
            }
            return result;
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static LogEntry ParseLine(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var timestamp = root.GetProperty("@t").GetDateTimeOffset();
        var level = root.TryGetProperty("@l", out var l)
            ? l.GetString() ?? "Information"
            : "Information";
        var template = root.TryGetProperty("@mt", out var mt)
            ? mt.GetString() ?? string.Empty
            : string.Empty;
        var exception = root.TryGetProperty("@x", out var x)
            ? x.GetString()
            : null;

        var props = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in root.EnumerateObject())
        {
            if (!prop.Name.StartsWith('@'))
                props[prop.Name] = prop.Value.Clone();
        }

        return new LogEntry(timestamp, level, template, RenderMessage(template, props), exception, props);
    }

    private static string RenderMessage(string template, Dictionary<string, JsonElement> props)
    {
        return PropertyPlaceholder().Replace(template, m =>
        {
            var name = m.Groups[1].Value;
            return props.TryGetValue(name, out var val)
                ? val.ValueKind == JsonValueKind.String ? val.GetString()! : val.ToString()
                : m.Value;
        });
    }

    [GeneratedRegex(@"\{(\w+)(?:[^}]*)?\}")]
    private static partial Regex PropertyPlaceholder();
}
