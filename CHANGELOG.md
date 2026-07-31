# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> Only the current release is kept on GitHub. Tags and release assets for `1.0.0` through `1.0.3`,
> and for the pre-semver `v5` and `v7`, have been removed; the sections below stay as the written
> record of what changed in each. Versioning started over at `1.0.0`, and `v5`/`v7` were numbered
> by CI run count and carried no compatibility meaning.

## [Unreleased]

## [1.0.4] - 2026-07-31

### Added

- `smoke-test.ps1` and `smoke-test.sh` ship inside every release zip. Start the simulator, run
  one of them, and it exercises the whole documented contract — 25 checks — printing PASS or
  FAIL for each and exiting non-zero if anything is wrong, so it doubles as a CI gate. Exits
  with 2, and says so, when the simulator is not reachable.
- README: a browser recipe. `/healthz` and `/version` open directly; for the POST endpoint,
  open `/healthz` first and use `fetch` from the console — being on the simulator's own origin
  is what avoids CORS, since no CORS policy is configured.

### Changed

- The container smoke test in CI now runs `scripts/smoke-test.sh` instead of a handful of inline
  curl calls, so the script that ships to users is exercised on every pull request and cannot
  rot unnoticed.

### Fixed

- `/version` reported the commit sha twice — `1.0.3+<sha>.<sha>` — because the release pipeline
  appends it to `InformationalVersion` and the SDK appended it again.
  `IncludeSourceRevisionInInformationalVersion` is now off.

## [1.0.3] - 2026-07-31

### Changed

- **Releases ship a runnable executable again.** 1.0.1 and 1.0.2 contained a DLL and a handful of
  JSON files, which is not something anyone can start. Each platform now gets a self-contained,
  single-file build — `TargetApiSimulator.exe` on Windows, `TargetApiSimulator` on Linux and
  macOS — that runs on a machine with no .NET installed at all. Roughly 50 MB, because the
  runtime is inside it.
- The small framework-dependent build is still published as
  `TargetApiSimulator-<version>-portable.zip` for anyone who already has the .NET 10 runtime.
- Console timestamps and the `Microsoft.AspNetCore` log filter are now defaults in code rather
  than only in `appsettings.json`, so the standalone executable logs identically when it runs
  with no files beside it. Configuration still overrides both.
- Packages no longer carry `web.config`, `aspnetcorev2_inprocess.dll` or the static web assets
  manifest — IIS hosting leftovers that made the download look like it needed assembling.
- The release pipeline fails if a platform build produces no launcher, replacing the earlier
  check that failed if one was present.

## [1.0.2] - 2026-07-31

### Fixed

- A missing `DOCKERHUB_USERNAME` / `DOCKERHUB_TOKEN` no longer fails the release. The 1.0.1 run
  published the tag, the zip and the GHCR image correctly, then went red on
  `docker login docker.io -u ""`. Docker Hub is now skipped with a note in the run summary when
  it is not configured, and the image still goes to GHCR.

## [1.0.1] - 2026-07-31

### Changed

- Release artifacts ship as **one portable zip** with a `.sha256` checksum instead of three
  per-platform ones, built with `-p:UseAppHost=false` so no executable is included. Microsoft
  Defender flagged the 1.0.0 Windows zip as `Trojan:Script/Wacatac.B!ml` and quarantined it
  mid-download — a machine-learning heuristic that fires on unsigned, freshly built executables
  with no download reputation. Without the launcher a per-RID publish produces an identical file
  set anyway, so three zips collapse into one. Run it with
  `dotnet TargetApiSimulator.dll --urls http://localhost:5000` on any platform.
- The release pipeline fails if an `.exe` ever reappears in the package, rather than publishing
  an artifact that will be flagged again.

### Added

- README: one-line snippets for PowerShell and bash that fetch and start the latest release
  through the GitHub API, so nothing has to be downloaded through a browser.

## [1.0.0] - 2026-07-31

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
- Release artifacts are per-platform zips (`win-x64`, `linux-x64`, `osx-arm64`) with `.sha256`
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

[Unreleased]: https://github.com/Mysttic/TargetApiSimulator/compare/v1.0.4...HEAD
[1.0.4]: https://github.com/Mysttic/TargetApiSimulator/releases/tag/v1.0.4
