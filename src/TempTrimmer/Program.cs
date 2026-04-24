using AcsSolutions.TempTrimmer.Api;
using AcsSolutions.TempTrimmer.Models;
using AcsSolutions.TempTrimmer.Services;
using Serilog;
using Serilog.Formatting.Compact;

// Bootstrap logger so startup errors are captured before DI is ready.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, _, lc) =>
    {
        var tempPath = Environment.ExpandEnvironmentVariables(
            ctx.Configuration[$"{TrimmerOptions.Section}:{nameof(TrimmerOptions.TempPath)}"]
            ?? "%TEMP%");

        var logDir = Path.Combine(tempPath, "TempTrimmer");
        Directory.CreateDirectory(logDir);

        lc.ReadFrom.Configuration(ctx.Configuration)
          .Enrich.FromLogContext()
          .WriteTo.Console()
          .WriteTo.File(
              new CompactJsonFormatter(),
              Path.Combine(logDir, "log-.jsonl"),
              rollingInterval: RollingInterval.Day,
              retainedFileCountLimit: null,   // TrimEngine manages log file lifetime via the normal deletion policy
              shared: false);
    });

    builder.Services.Configure<TrimmerOptions>(
        builder.Configuration.GetSection(TrimmerOptions.Section));

    // Expose IConfigurationRoot so ConfigPersistenceService can call Reload() after writing.
    builder.Services.AddSingleton(sp =>
        (IConfigurationRoot)sp.GetRequiredService<IConfiguration>());

    builder.Services.AddSingleton<TrimState>();
    builder.Services.AddTransient<TrimEngine>();
    builder.Services.AddSingleton<ConfigPersistenceService>();
    builder.Services.AddTransient<LogReaderService>();
    builder.Services.AddTransient<ApiKeyEndpointFilter>();
    builder.Services.AddHostedService<TrimmerBackgroundService>();

    builder.Services.AddRazorPages();

    var app = builder.Build();

    // Support running as a Kudu virtual application (e.g. /AcsSolutions.TempTrimmer).
    var pathBase = builder.Configuration["TempTrimmer:PathBase"];
    if (!string.IsNullOrWhiteSpace(pathBase))
        app.UsePathBase(pathBase);

    app.UseStaticFiles();
    app.UseRouting();
    app.MapRazorPages();

    TrimEndpoints.Map(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
