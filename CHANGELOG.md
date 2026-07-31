# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> Releases `v5` and `v7` predate this file. They were numbered by CI run count, carry no
> compatibility meaning, and are not described here. Versioning starts over at `1.0.0`.

## [Unreleased]

First release under tag-driven versioning. Promote this section to `## [1.0.0] - YYYY-MM-DD`
when the `develop -> master` pull request is merged and the tag is pushed.

### Added

- `GET /healthz` returning `{"status":"ok"}`.
- `GET /version` returning the version the binary was built with.
- Opt-in `X-Sim-*` control headers for simulating a misbehaving downstream service:
  `X-Sim-Delay-Ms` delays the response (clamped to `Simulator:MaxDelayMs`, default 60000) and
  `X-Sim-Status` forces a status code without running the endpoint. Both are off unless
  `Simulator:EnableControlHeaders` is set. When either takes effect the response carries
  `X-Sim-Applied`, so a test can tell an injected failure apart from a real one.
- UTC timestamps with millisecond precision on every console log line. The default console
  formatter emits none, which made it impossible to correlate the stub's log with the system
  under test, or to confirm from the log that an injected delay actually happened.
- Automated test suite: 131 tests covering the validation contract, routing, encodings, nesting
  depth, control headers and the response shape.
- `.github/workflows/ci.yml` — build, test, format check and vulnerable-package scan on every
  pull request into `develop` or `master`, plus a container build and smoke test.
- `VERSION.md` as the single source of truth for the released version. Bumping it and merging
  into `master` creates the tag and publishes; merging without touching it does nothing.
- `.github/workflows/release.yml` — release driven by `VERSION.md`: multi-platform zips with
  checksums, the git tag, a GitHub Release with generated notes, and images pushed to GHCR and
  Docker Hub. A `workflow_dispatch` run builds everything without publishing.
- `CONTRIBUTING.md`, `CHANGELOG.md`, `LICENSE` (MIT), `.editorconfig`, `.dockerignore`,
  `TargetApiSimulator.http`, Dependabot config, a pull request template, and branch protection
  rulesets as JSON.
- README: validation contract table, port table, image tag policy, versioning guide, and
  terminal captures of the service running.

### Changed

- Target framework `net8.0` -> `net10.0`. .NET 8 leaves support on 2026-11-10.
- Both response branches now declare `Content-Type: application/json; charset=utf-8`. Previously
  the JSON error object arrived untyped and clients could not dispatch on it.
- Request logging moved from `Console.WriteLine` to `ILogger`, and the echoed body is truncated
  at 512 characters. Log levels in `appsettings.json` now actually apply.
- Maximum request body reduced from Kestrel's 30 MB default to 1 MB. Bodies above the limit get
  `413` instead of being buffered.
- JSON validation extracted into `JsonValidator` with explicit `JsonDocumentOptions`
  (`MaxDepth` 64, no comments, no trailing commas), so the accept/reject contract is pinned
  rather than inherited from framework defaults.
- Project moved to `src/TargetApiSimulator/`; tests live in `tests/TargetApiSimulator.Tests/`.
- Dockerfile rewritten: one `publish` stage instead of a duplicated build, runs as the
  unprivileged `uid 1654`, OCI labels, and the version passed in as a build argument.
- Release artifacts are now per-platform (`win-x64`, `linux-x64`, `osx-arm64`) with `.sha256`
  checksums, named after the version.
- `appsettings.Development.json` now lowers log levels instead of duplicating
  `appsettings.json` verbatim.
- Development-only launch profiles reduced to `http` and `https`.

### Fixed

- Removed `<RuntimeIdentifier>linux-x64</RuntimeIdentifier>`, which made `dotnet run` fail on
  Windows and macOS and shipped a Linux-only executable in every release, contradicting the
  README's instruction to download and run it.
- `BadHttpRequestException` and `OperationCanceledException` are handled. An oversized body or a
  client disconnect used to write a full stack trace to the log on every occurrence.
- README told users to run the container with `-p 5000:80`; the image listens on 8080, so the
  documented command could never connect.
- Release archives no longer nest everything under a `publish/` directory.
- `EXPOSE 8081` removed from the image — it advertised an HTTPS listener that was never
  configured.
- `Program.cs` re-encoded from CP1250 to UTF-8. The file contained bytes that decoded differently
  depending on the machine's active code page.

### Removed

- `.github/workflows/dotnet.yml`, replaced by `ci.yml` and `release.yml`. It ran only on pushes
  to `master`, had no test step, and tagged releases `v<CI run number>`.
- The `PAT_TOKEN` secret, which was passed as `env: GITHUB_TOKEN` and silently ignored because
  the action reads its `token` input first.
- `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` package reference and the unused
  `UserSecretsId`.

### Security

- Workflows declare least-privilege `permissions`; write access is granted per job.
- Container no longer runs as root.
- Every pull request runs `dotnet list package --vulnerable --include-transitive` and fails on a
  hit. CLI output is forced to English so the check cannot silently pass on a localised runner.
