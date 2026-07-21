# TempTrimmer

[![CI](https://github.com/alasdaircs/TempTrimmer/actions/workflows/ci.yml/badge.svg)](https://github.com/alasdaircs/TempTrimmer/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AcsSolutions.TempTrimmer.svg)](https://www.nuget.org/packages/AcsSolutions.TempTrimmer/)
[![Licence: MIT](https://img.shields.io/badge/Licence-MIT-blue.svg)](LICENSE.md)

Azure Windows App Service **Site Extension** that keeps the site's `%TEMP%` folder under control by automatically deleting files that are too old or are pushing the total size over a configurable limit.

---

## How it works

Two independent conditions are evaluated on a configurable schedule (default: every 15 minutes):

| Condition | Description |
|---|---|
| **Age** | Files whose last-write time is older than `MaxAge` (default: 72 hours) are always deleted. |
| **Size quota** | If the total temp folder size exceeds `MaxTotalSizeMb` (default: 256 MB), the oldest files are deleted first until the total falls within the limit. |

A file is deleted if it satisfies **either** condition. Locked files are skipped gracefully and the error is recorded in the run log.

> **⚠️ TempTrimmer installs in dry-run mode and deletes nothing until you arm it.** See [Arming the trimmer](#arming-the-trimmer).

---

## Features

- **Admin dashboard** — live temp folder stats (total size, file count, oldest/newest file), last-run summary with per-file deletion details.
- **Configuration UI** — change thresholds and scan interval at runtime; changes are written to `%HOME%\data\TempTrimmer\settings.json` (survives upgrades) and reloaded immediately without a restart.
- **Log viewer** — filterable, paginated view of structured log entries read directly from the rolling JSONL log files.
- **REST trigger** — `POST /api/trim` for integration with Azure Logic Apps, CI pipelines, or scheduled tasks.
- **API key auth** — configurable `X-Api-Key` header guard on the REST endpoint.
- **Structured logging** — Serilog CLEF/JSONL, rolling daily, stored in `%TEMP%\TempTrimmer\` with a 7-day retention cap (and additionally subject to the same deletion policy once armed).

---

## Requirements

- Azure **Windows** App Service (any tier that supports Site Extensions)
- **.NET 10** runtime (pre-installed on Azure App Service for Windows)

---

## Installation

### Via the Kudu gallery

1. Open the SCM console: `https://<your-site>.scm.azurewebsites.net`
2. Go to **Site extensions** → search for **AcsSolutions.TempTrimmer** → **Install**.
3. Restart the site when prompted.
4. The dashboard will be at `https://<your-site>.scm.azurewebsites.net/AcsSolutions.TempTrimmer/`.

### Via NuGet CLI

```powershell
nuget install AcsSolutions.TempTrimmer -OutputDirectory "$env:HOME\SiteExtensions"
```

Then restart your App Service.

---

## Arming the trimmer

**A fresh install runs in dry-run mode: it scans, reports and logs what it *would* delete, but deletes nothing.** This is deliberate — it lets you review the candidate list and exclusions before any file is touched — but it means temp hygiene is **not** in place until you arm it.

While dry-run is active:

- the dashboard shows a prominent warning banner, and
- every scan writes a `Warning`-level log line: `DRY RUN: would have deleted N file(s) (X MB) — no files were deleted…`

To arm the trimmer, either:

1. **(Recommended, especially for fleets/IaC)** add the App Setting `TempTrimmer__DryRun` = `false` in the Azure portal or your ARM/Bicep template — this survives extension upgrades and re-installs; or
2. turn off **Dry-run mode** on the extension's Configuration page (saved to `%HOME%\data\TempTrimmer\settings.json`, which also survives upgrades).

---

## Configuration

Configuration is layered; later sources override earlier ones:

1. **Shipped defaults** — `appsettings.json` inside the extension directory (replaced on every upgrade or re-install; don't edit it).
2. **UI-saved settings** — `%HOME%\data\TempTrimmer\settings.json`, written by the Configuration page. `%HOME%` is the durable shared content root, so these survive upgrades and re-installs.
3. **Azure App Settings** (environment variables, `TempTrimmer__*` with `__` as the section separator) — always win, and are the recommended route for fleets managed via ARM/Bicep. The Configuration page shows env-overridden fields as read-only.

| Key | Type | Default | Description |
|---|---|---|---|
| `TempTrimmer:DryRun` | `bool` | `true` | **When `true` (the default) no files are ever deleted** — candidates are only reported. See [Arming the trimmer](#arming-the-trimmer) |
| `TempTrimmer:MaxAge` | `TimeSpan` | `3.00:00:00` | Delete files older than this |
| `TempTrimmer:MaxTotalSizeMb` | `long` | `256` | Size quota in MB — see [sizing guidance](#sizing-maxtotalsizemb-on-multi-app-plans) below |
| `TempTrimmer:ScanInterval` | `TimeSpan` | `00:15:00` | Background scan frequency |
| `TempTrimmer:ExcludedFolders` | `string[]` | `["/jobs*"]` | Glob patterns (relative to the temp root) for folders to skip entirely |
| `TempTrimmer:ExcludedFiles` | `string[]` | `["/applicationhost.config"]` | Glob patterns for files to skip regardless of age or size |
| `TempTrimmer:ApiKey` | `string` | *(empty)* | Required `X-Api-Key` value for `POST /api/trim`; empty = no extra auth (the endpoint is still behind Kudu/SCM authentication) |
| `TempTrimmer:TempPath` | `string` | `%TEMP%` | Folder to scan; environment variables are expanded |
| `TempTrimmer:PathBase` | `string` | `/AcsSolutions.TempTrimmer` | Path base when hosting as a Kudu virtual application |

### Sizing `MaxTotalSizeMb` on multi-app plans

Each app and slot sandbox gets its own `%TEMP%`, but **all apps and slots on an instance share one SKU-dependent local temp quota** (roughly 11–21 GB on smaller instances). The quota that matters is therefore the *sum* across every install on the plan. As a rule of thumb:

> `MaxTotalSizeMb` ≈ instance temp quota ÷ (number of apps + slots on the plan), with headroom.

For example, 17 sites/slots on an instance with a 15 GB temp quota should each be capped well below 900 MB. The default of 256 MB is a conservative backstop — the 72-hour age rule does most of the day-to-day work.

### Overriding via Azure App Settings

In the Azure portal → your App Service → **Configuration** → **Application settings**:

| Name | Example value |
|---|---|
| `TempTrimmer__DryRun` | `false` |
| `TempTrimmer__MaxAge` | `1.00:00:00` |
| `TempTrimmer__MaxTotalSizeMb` | `512` |
| `TempTrimmer__ApiKey` | `your-secret-key` |
| `TempTrimmer__ExcludedFolders__0` | `/jobs*` |

`TimeSpan` values follow the standard format `d.hh:mm:ss` (e.g. `3.00:00:00` = 3 days, `00:30:00` = 30 minutes). Array settings use an index suffix (`__0`, `__1`, …).

---

## REST API

### `POST /api/trim`

Triggers an immediate trim run synchronously and returns the result.

**Request headers:**

```
X-Api-Key: <your-api-key>
```

*(Required only when `TempTrimmer:ApiKey` is configured.)*

The endpoint is hosted on the SCM (Kudu) site, so it is always behind Kudu's own authentication; the API key is defence-in-depth on top of that, not the only barrier. When `ApiKey` is empty a warning is logged on each unauthenticated call.

**Success — `200 OK`:**

```json
{
  "startedAt": "2026-04-24T20:00:00Z",
  "completedAt": "2026-04-24T20:00:01.234Z",
  "deletedFiles": [
    {
      "path": "D:\\local\\Temp\\some_file.tmp",
      "sizeBytes": 4096,
      "lastWriteTimeUtc": "2026-04-21T10:00:00Z",
      "reason": "TooOld"
    }
  ],
  "errors": [],
  "totalBytesFreed": 4096,
  "duration": "00:00:01.234"
}
```

`reason` is either `"TooOld"` or `"OverQuota"`.

**`409 Conflict`** — A trim run is already in progress.  
**`401 Unauthorized`** — Missing or invalid API key.

---

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026, or VS Code with the C# Dev Kit extension

### Build & test

```bash
dotnet build
dotnet test
```

### Run locally

```bash
dotnet run --project src/TempTrimmer
```

The dashboard is available at `https://localhost:5001/`.

> **Warning:** When running locally, `%TEMP%` resolves to your own user temp folder. Set a conservative `MaxAge` and `MaxTotalSizeMb` to avoid deleting files you need.

### Build the NuGet package

```bash
dotnet pack src/TempTrimmer/TempTrimmer.csproj -c Release -o nupkg
```

`dotnet pack` triggers `dotnet publish` internally and bundles the output into the NuGet `content/` folder alongside `applicationHost.xdt`.

### Releasing a new version

1. Update `<Version>` in `src/TempTrimmer/TempTrimmer.csproj`.
2. Commit and push.
3. Tag the commit:
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```
4. CI builds, packs, and publishes to NuGet.org automatically using the `NUGET_API_KEY` repository secret.

---

## Code signing

The package is currently **unsigned**. A code-signing certificate is under consideration; once obtained, a signing step will be added to the CI pipeline.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Security

See [SECURITY.md](SECURITY.md).

## Licence

[MIT](LICENSE.md) © Alasdair Cunningham-Smith 2026
