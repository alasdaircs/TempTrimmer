# TempTrimmer — Azure Windows App Service Site Extension

## Project brief

A .NET 10 Web App published as an **Azure Windows App Service Site Extension** whose sole purpose is to manage temp folder size by deleting files.

### Deletion policy

Two independent conditions, either of which makes a file a deletion candidate:

1. **Age** — file is older than a configurable age threshold.
2. **Size quota** — the oldest N files that must be removed to keep the total temp-folder consumption (for this site/slot) under a configurable size threshold.

Files meeting either condition are deleted.

### Solution structure

| Project | Purpose |
|---|---|
| `src/TempTrimmer` | .NET 10 Web App — the Site Extension |
| `tests/TempTrimmer.Tests` | xUnit test project |

Solution file: `TempTrimmer.slnx` (Visual Studio 2026 `.slnx` format)

### Repository

`https://github.com/alasdaircs/TempTrimmer`

### NuGet / Site Extension packaging

The NuGet package must satisfy all Azure Windows App Service Site Extension requirements:

- `tags` must include `AzureSiteExtension`
- `applicationHost.xdt` IIS transform included
- Correct package structure (`content/` and/or `tools/` folders)
- Unique, descriptive package ID
- Proper `<dependencies>` targeting .NET 10
- All required metadata fields (authors, licenseUrl / license expression, projectUrl, iconUrl/icon, description, releaseNotes)

Owner has a NuGet account authenticated via Microsoft 365 Entra (no code-signing certificate yet — may obtain one later).

### CI / CD

- GitHub Actions CI YAML (build + test on every PR and push to main)
- Dependabot enabled (`dependabot.yml`)

### Resolved design decisions

| Decision | Resolution |
|---|---|
| Temp folder target | `%TEMP%` (per-sandbox on Azure App Service) |
| Trigger | Background `IHostedService` (15 min default) + `POST /api/trim` HTTP endpoint |
| UI | Dashboard (stats, last run, Run Now), Configuration form, Log viewer |
| Configuration | Layered: shipped `appsettings.json` → UI-saved `%HOME%\data\TempTrimmer\settings.json` (durable; survives upgrades/re-installs) → environment variables. `IOptionsSnapshot` in pages, `IOptionsMonitor` in background service; `ConfigPersistenceService` writes the durable file and calls `IConfigurationRoot.Reload()`. Config UI shows effective values and marks env-overridden fields read-only |
| NuGet package ID | `AcsSolutions.TempTrimmer` |
| Default MaxAge | 72 hours (`TimeSpan`) |
| Default MaxTotalSizeMb | 256 (instance temp quota is shared by all apps/slots on the plan — default must be safe on dense plans) |
| Default DryRun | `true`, but loud: dashboard banner + per-scan `Warning` log until armed |
| Logging | Serilog JSONL (CLEF) rolling daily to `%TEMP%\TempTrimmer\log-.jsonl`, `retainedFileCountLimit: 7` (bounded even in dry-run) |
| Package `Title` | ASCII only — non-ASCII is mangled to U+FFFD in the pack → NuGet → Kudu metadata chain |

### API Key authentication

`POST /api/trim` requires `X-Api-Key` header when `TempTrimmer:ApiKey` is set.
If the key is empty, the endpoint is unauthenticated (a warning is logged).

### Deploying a new version (NuGet)

1. Tag the commit: `git tag v1.x.x && git push origin v1.x.x`
2. CI runs, builds the NuGet package, and publishes it to NuGet.org using the `NUGET_API_KEY` repository secret.

### Notes for future work

- Add a `TempTrimmer:PathBase` app setting to `/AcsSolutions.TempTrimmer` for correct Kudu link generation.
- Code-signing certificate: add `SignTool` step to CI once a certificate is obtained.
- Consider adding ANTIFORGERY token validation to the `/api/trim` endpoint for CSRF protection if ever called from a browser context.

## Conventions

- British English in all user-facing text, comments, and documentation.
- No unnecessary comments in code — only add when the WHY is non-obvious.
- No trailing summaries in Claude responses (user reads the diff).
