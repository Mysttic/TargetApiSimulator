# Version

1.0.3

<!--
  This file is the single source of truth for the released version.

  Bump the number above, merge it into master, and the release pipeline creates the matching
  git tag and publishes the release. Nothing is tagged by hand.

  Rules:
  - The version must sit alone on its own line, in MAJOR.MINOR.PATCH form. A pre-release
    suffix such as 1.1.0-rc.1 is allowed and is published as a GitHub pre-release.
  - Merging into master without changing this file does nothing: the tag already exists, so
    the pipeline stops before publishing anything.
  - Record what changed in CHANGELOG.md under the same number.

  What each part means for this service:
  - MAJOR: the HTTP contract changed (a status code, a response body shape, or a route).
  - MINOR: a new endpoint or newly accepted input, backwards compatible.
  - PATCH: bug fix, dependency bump, documentation, CI.
-->
