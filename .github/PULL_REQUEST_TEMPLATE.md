## What

<!-- One sentence. This ends up in the release notes, so write it from the point of view of
     someone who uses the simulator. -->

## Contract impact

- Endpoint(s):
- Status code changed: yes / no
- Response body changed: yes / no
- If either is "yes" -> a MINOR or MAJOR bump is required, plus a note in the release notes.

## Checklist

- [ ] Targets `develop` (not `master`), unless this is a release or a hotfix
- [ ] `dotnet test` passes locally
- [ ] `dotnet format --verify-no-changes` is clean
- [ ] README updated if user-visible behaviour changed
- [ ] Validation contract table in README updated if the accepted/rejected set changed
- [ ] If this should ship: `VERSION.md` bumped and `CHANGELOG.md` has a section for that number

## How to verify

```bash
curl -i -X POST http://localhost:5274/api/target \
  -H 'Content-Type: application/json' -d '{"key":"value"}'
```
