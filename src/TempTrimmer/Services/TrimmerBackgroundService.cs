using AcsSolutions.TempTrimmer.Models;
using Microsoft.Extensions.Options;

namespace AcsSolutions.TempTrimmer.Services;

public sealed class TrimmerBackgroundService : BackgroundService
{
    private readonly TrimEngine _engine;
    private readonly TrimState _state;
    private readonly IOptionsMonitor<TrimmerOptions> _options;
    private readonly ILogger<TrimmerBackgroundService> _logger;

    public TrimmerBackgroundService(
        TrimEngine engine,
        TrimState state,
        IOptionsMonitor<TrimmerOptions> options,
        ILogger<TrimmerBackgroundService> logger)
    {
        _engine = engine;
        _state = state;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Allow the application to finish starting before the first run.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_state.TrySetRunning())
            {
                TrimResult? result = null;
                try
                {
                    result = _engine.Execute(_options.CurrentValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduled trim run failed");
                    result = new TrimResult
                    {
                        StartedAt = DateTimeOffset.UtcNow,
                        CompletedAt = DateTimeOffset.UtcNow,
                        Errors = [ex.Message],
                    };
                }
                finally
                {
                    _state.SetCompleted(result!);
                }
            }
            else
            {
                _logger.LogDebug("Scheduled trim skipped — a run is already in progress.");
            }

            try
            {
                await Task.Delay(_options.CurrentValue.ScanInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
