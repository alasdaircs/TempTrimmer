using AcsSolutions.TempTrimmer.Models;
using AcsSolutions.TempTrimmer.Services;
using Microsoft.Extensions.Options;

namespace AcsSolutions.TempTrimmer.Api;

public static class TrimEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/trim", async (
            TrimEngine engine,
            TrimState state,
            DeletionLogService deletionLog,
            IOptionsMonitor<TrimmerOptions> options,
            ILogger<Program> logger) =>
        {
            if (!state.TrySetRunning())
                return Results.Conflict(new { message = "A trim run is already in progress." });

            TrimResult? result = null;
            try
            {
                result = engine.Execute(options.CurrentValue);

                if (!result.IsDryRun && result.DeletedFiles.Count > 0)
                    await deletionLog.AppendAsync(result.DeletedFiles, result.CompletedAt);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Trim run failed via API");
                result = new TrimResult
                {
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow,
                    Errors = [ex.Message],
                };
                return Results.Problem(ex.Message);
            }
            finally
            {
                if (result is not null)
                    state.SetCompleted(result);
                else
                    state.SetCompleted(new TrimResult
                    {
                        StartedAt = DateTimeOffset.UtcNow,
                        CompletedAt = DateTimeOffset.UtcNow,
                    });
            }
        })
        .AddEndpointFilter<ApiKeyEndpointFilter>()
        .WithName("TriggerTrim")
        .WithDescription("Triggers an immediate temp folder trim. Requires X-Api-Key header when an API key is configured.");
    }
}
