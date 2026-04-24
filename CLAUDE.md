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

### Open questions (to be resolved before implementation)

- **Temp folder target**: which folder(s) to monitor? `D:\local\Temp`? Configurable path?
- **Trigger mechanism**: background hosted service (timer), HTTP on-demand endpoint, or both?
- **UI**: admin page in Kudu SCM, API-only, or full dashboard?
- **Configuration source**: Azure App Settings (env vars), JSON config file, or both?
- **NuGet package ID / namespace**: e.g. `TempTrimmer`, `ACS.TempTrimmer`, etc.
- **Default values**: max file age, max total size threshold

## Conventions

- British English in all user-facing text, comments, and documentation.
- No unnecessary comments in code — only add when the WHY is non-obvious.
- No trailing summaries in Claude responses (user reads the diff).
