# Contributing

## Branch model

```
feature/xyz ──squash──> develop ──merge PR──> master ──tag vX.Y.Z──> Release
                            ^                                            │
                            └─────── merge master -> develop ────────────┘
                                        (manually, after the release)
```

- **`develop`** — everything merged, always green, nothing released yet. This is the default
  branch and the target for every `feature/*`, `fix/*`, `chore/*` branch and every Dependabot
  PR. Merged with squash. Nothing is published from it except test results.
- **`master`** — what is released. It only ever receives a merge commit from a `develop → master`
  pull request, or from a `hotfix/*` branch. Every `v*` tag on master is a release.
- **Tagging is a human action, not something CI does.** The tag is the decision "this is a
  release". Keeping it manual is what stops meaningless version numbers from appearing.
- **Hotfixes branch off `master`, not `develop`** — develop holds unreleased features you do not
  want to ship in an emergency patch. After tagging, merge master back into develop
  (`git checkout develop && git merge master`). Skipping the back-merge is the classic way to
  make a fixed bug reappear two releases later.
- **No `release/*` branches.** They exist to stabilise a release while new features keep landing
  on develop. With one maintainer and one endpoint, the `develop → master` PR is opened and
  merged within the hour. Revisit this the first time you need to ship 1.3.0 without a feature
  that already landed on develop.

## Versioning and releasing

Semver, driven by the git tag on `master`. Nothing else sets the version — CI reads it from the
tag name, and a build with no version supplied reports `1.0.0`.

- **MAJOR** — the HTTP contract changed: a status code, the shape of a response body, or a route.
- **MINOR** — a new endpoint, or a new accepted input, backwards compatible.
- **PATCH** — bug fix, dependency bump, documentation, CI.

The validation contract table in the README is the reference for what counts as a breaking
change. If a row in that table changes, so does the major or minor number.

### Cutting a release

No tag is ever created by hand. [VERSION.md](./VERSION.md) drives everything.

1. On a branch off `develop`, bump the number in `VERSION.md` and rename the `## [Unreleased]`
   section in [CHANGELOG.md](./CHANGELOG.md) to `## [X.Y.Z] - YYYY-MM-DD`, opening a fresh empty
   `## [Unreleased]` above it. Merge that into `develop`.
2. Open a pull request from `develop` into `master`. CI validates `VERSION.md` and writes to the
   run summary whether merging will publish, and under which number.
3. Merge with a **merge commit** (not squash — `master` keeps real merge commits).
4. `release.yml` runs on that push to `master`. It reads `VERSION.md`, confirms `vX.Y.Z` does not
   already exist, builds and tests, publishes zips for `win-x64`, `linux-x64` and `osx-arm64`
   with `.sha256` files, **creates the tag**, publishes the GitHub Release, and pushes images
   tagged `X.Y.Z`, `X.Y` and `latest` to GHCR and Docker Hub.
5. Back-merge so develop does not fall behind:

   ```bash
   git checkout develop && git merge master && git push
   ```

Merging into `master` without touching `VERSION.md` is safe and does nothing: the tag already
exists, so the pipeline stops at the first job. That check is what makes the release idempotent —
re-running the workflow on the same commit cannot produce a second release.

Hotfixes branch off `master`, bump `VERSION.md` the same way, and are back-merged into `develop`.

### Why the tag is created inside the release job

A tag pushed using the default `GITHUB_TOKEN` does not trigger further workflow runs — GitHub
blocks that to prevent recursion. A design where one workflow pushes the tag and another listens
for it would create the tag and then silently do nothing, unless a personal access token were
introduced. Doing the tagging and the publishing in a single run avoids needing one.

### Dry run

`release.yml` can be started manually from the Actions tab. A `workflow_dispatch` run builds and
tests and produces the artifacts, but never tags, releases or pushes an image — useful for
checking the pipeline without publishing.

### Setting the version by hand

| Situation | Command |
|---|---|
| Local build with a version | `dotnet build -p:Version=1.2.3` |
| Container | `docker build --build-arg VERSION=1.2.3 .` |
| Check what a binary reports | `curl http://localhost:5274/version` |

The container needs `--build-arg` because `.dockerignore` excludes `.git`, so nothing inside the
build context can derive the version on its own.

### Repository settings as code

The branch and tag protection rules live in [.github/rulesets](./.github/rulesets) so they can be
recreated:

```bash
gh api --method POST /repos/Mysttic/TargetApiSimulator/rulesets --input .github/rulesets/protect-develop.json
```

The only required status check is `ci-required`; it must have run at least once before the rule
can reference it.

## Local development

Requires the .NET 10 SDK.

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format TargetApiSimulator.sln --verify-no-changes --severity warn
```

Run the app:

```bash
dotnet run --project src/TargetApiSimulator
```

It listens on `http://localhost:5274`. Requests are in
[TargetApiSimulator.http](./TargetApiSimulator.http).

## Tests

`tests/TargetApiSimulator.Tests` holds characterisation tests: each assertion pins the behaviour
the service has today, so a refactor shows up as a changed assertion in the diff rather than as a
silent regression. Tests whose name contains `Currently` deliberately encode a known defect — when
the defect is fixed, the assertion flips and the prefix goes away.

Two things cannot be covered by `WebApplicationFactory` and belong in the container smoke test in
CI instead:

- **Request body size limits.** `TestServer` does not enforce `MaxRequestBodySize`; a body over
  the 1 MB limit returns `200` there and `413` on real Kestrel.
- **Port binding.** The container listens on 8080; only a real `docker run` proves it.

## CI

Every pull request into `develop` or `master` runs `.github/workflows/ci.yml`: build, test,
formatting check, vulnerable-package scan, plus a container build and smoke test.

The only required status check is **`ci-required`**. It aggregates the other jobs, so the
protection rules do not need updating when a job is added or a matrix changes.
