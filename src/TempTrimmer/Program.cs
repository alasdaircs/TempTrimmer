using AcsSolutions.TempTrimmer.Api;
using AcsSolutions.TempTrimmer.Models;
using AcsSolutions.TempTrimmer.Services;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using Serilog;
using Serilog.Formatting.Compact;

// Bootstrap logger so startup errors are captured before DI is ready.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // UI-saved settings live outside the extension folder (%HOME%\data on App Service) so they
    // survive extension upgrades and ARM re-installs. Layered after the shipped appsettings.json
    // but before environment variables, so App Settings (TempTrimmer__*) always win.
    var uiSettingsPath = ConfigPersistenceService.GetSettingsFilePath(builder.Environment.ContentRootPath);
    Directory.CreateDirectory(Path.GetDirectoryName(uiSettingsPath)!);
    var uiSettingsSource = new JsonConfigurationSource
    {
        Path = Path.GetFileName(uiSettingsPath),
        FileProvider = new PhysicalFileProvider(Path.GetDirectoryName(uiSettingsPath)!),
        Optional = true,
        ReloadOnChange = true,
    };
    var configSources = ((IConfigurationBuilder)builder.Configuration).Sources;
    var insertAt = 0;
    for (var i = 0; i < configSources.Count; i++)
        if (configSources[i] is JsonConfigurationSource) insertAt = i + 1;
    configSources.Insert(insertAt, uiSettingsSource);

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
              // Hard retention cap: in dry-run mode TrimEngine deletes nothing, so without this
              // the tool's own logs would grow without bound.
              retainedFileCountLimit: 7,
              shared: false);
    });

    builder.Services.Configure<TrimmerOptions>(
        builder.Configuration.GetSection(TrimmerOptions.Section));

    // Expose IConfigurationRoot so ConfigPersistenceService can call Reload() after writing.
    builder.Services.AddSingleton(sp =>
        (IConfigurationRoot)sp.GetRequiredService<IConfiguration>());

    builder.Services.AddSingleton<TrimState>();
    builder.Services.AddSingleton<DeletionLogService>();
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

    // icon.png lives at the content root (not wwwroot) so the static files middleware
    // can't serve it; expose it via a dedicated endpoint instead.
    app.MapGet("/icon.png", (IWebHostEnvironment env) =>
        Results.File(Path.Combine(env.ContentRootPath, "icon.png"), "image/png"));

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
