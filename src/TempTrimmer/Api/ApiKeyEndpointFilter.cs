using AcsSolutions.TempTrimmer.Models;
using Microsoft.Extensions.Options;

namespace AcsSolutions.TempTrimmer.Api;

public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    private readonly IOptionsMonitor<TrimmerOptions> _options;
    private readonly ILogger<ApiKeyEndpointFilter> _logger;

    public ApiKeyEndpointFilter(
        IOptionsMonitor<TrimmerOptions> options,
        ILogger<ApiKeyEndpointFilter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var configuredKey = _options.CurrentValue.ApiKey;

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            _logger.LogWarning("API key is not configured — /api/trim is unauthenticated.");
            return await next(context);
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var provided)
            || provided != configuredKey)
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}
