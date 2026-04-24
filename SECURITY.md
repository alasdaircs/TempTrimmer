# Security Policy

## Supported versions

Only the latest published version of `AcsSolutions.TempTrimmer` on NuGet.org receives security fixes.

| Version | Supported |
|---|---|
| Latest | ✅ |
| Older  | ❌ |

## Reporting a vulnerability

**Please do not report security vulnerabilities via a public GitHub issue.**

Instead, e-mail **security@acs-solutions.co.uk** with:

- A description of the vulnerability and its potential impact.
- Steps to reproduce or a proof-of-concept (if safe to share).
- The version(s) affected.

You will receive an acknowledgement within **5 business days**. If the issue is confirmed, a patched release will be prepared as soon as practicable and you will be credited (unless you prefer to remain anonymous).

## Scope

This policy covers the NuGet package `AcsSolutions.TempTrimmer` and the code in this repository. It does not cover the underlying Azure App Service platform or third-party dependencies (report those to their respective maintainers).

## Notes on the REST endpoint

The `POST /api/trim` endpoint should always be protected by an API key (`TempTrimmer:ApiKey`) in production. When hosted as a Kudu Site Extension the extension already sits behind Azure's SCM authentication layer, but defence in depth is recommended.
