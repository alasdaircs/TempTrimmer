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
| **Size quota** | If the total temp folder size exceeds `MaxTotalSizeMb` (default: 1 GB), the oldest files are deleted first until the total falls within the limit. |

A file is deleted if it satisfies **either** condition. Locked files are skipped gracefully and the error is recorded in the run log.

---

## Features

- **Admin dashboard** — live temp folder stats (total size, file count, oldest/newest file), last-run summary with per-file deletion details.
- **Configuration UI** — change thresholds and scan interval at runtime; changes are written to `appsettings.json` and reloaded immediately without a restart.
- **Log viewer** — filterable, paginated view of structured log entries read directly from the rolling JSONL log files.
- **REST trigger** — `POST /api/trim` for integration with Azure Logic Apps, CI pipelines, or scheduled tasks.
- **API key auth** — configurable `X-Api-Key` header guard on the REST endpoint.
- **Structured logging** — Serilog CLEF/JSONL, rolling daily, stored in `%TEMP%\TempTrimmer\` and subject to the same deletion policy.

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

## Configuration

Settings live in `appsettings.json` inside the extension directory. Any setting can be overridden by an Azure App Setting (environment variable) using `__` (double underscore) as the section separator.

| Key | Type | Default | Description |
|---|---|---|---|
| `TempTrimmer:MaxAge` | `TimeSpan` | `3.00:00:00` | Delete files older than this |
| `TempTrimmer:MaxTotalSizeMb` | `long` | `1024` | Size quota in MB |
| `TempTrimmer:ScanInterval` | `TimeSpan` | `00:15:00` | Background scan frequency |
| `TempTrimmer:ApiKey` | `string` | *(empty)* | Required `X-Api-Key` value for `POST /api/trim`; empty = unauthenticated |
| `TempTrimmer:TempPath` | `string` | `%TEMP%` | Folder to scan; environment variables are expanded |
| `TempTrimmer:PathBase` | `string` | *(empty)* | Set to `/AcsSolutions.TempTrimmer` when hosting as a Kudu virtual application |

### Overriding via Azure App Settings

In the Azure portal → your App Service → **Configuration** → **Application settings**:

| Name | Example value |
|---|---|
| `TempTrimmer__MaxAge` | `1.00:00:00` |
| `TempTrimmer__MaxTotalSizeMb` | `512` |
| `TempTrimmer__ApiKey` | `your-secret-key` |

`TimeSpan` values follow the standard format `d.hh:mm:ss` (e.g. `3.00:00:00` = 3 days, `00:30:00` = 30 minutes).

---

## REST API

### `POST /api/trim`

Triggers an immediate trim run synchronously and returns the result.

**Request headers:**

```
X-Api-Key: <your-api-key>
```

*(Required only when `TempTrimmer:ApiKey` is configured.)*

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

[MIT](LICENSE.md) © ACS Solutions 2026
