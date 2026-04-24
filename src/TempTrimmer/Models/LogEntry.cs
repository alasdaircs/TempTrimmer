using System.Text.Json;

namespace AcsSolutions.TempTrimmer.Models;

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string MessageTemplate,
    string RenderedMessage,
    string? Exception,
    IReadOnlyDictionary<string, JsonElement> Properties);
