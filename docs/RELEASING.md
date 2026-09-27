# Releasing PC Manager

Every push to a PR or `main` already builds and compiles the installer (see `.github/workflows/ci.yml`,
job `build-installer`) and uploads `PCManager-Setup-<version>.exe` as a workflow artifact, so you can
sanity-check the installer before ever cutting a release. A real release is only produced by pushing a
`vX.Y.Z` tag.

## Steps

1. **Bump the version.** Edit `Directory.Build.props` and change `<Version>` to the new version
   (semantic versioning, no `v` prefix, e.g. `0.2.0`). This is the single source of the version -
   it flows into the app's assembly/informational version (shown in the sidebar footer) and into
   the installer's `AppVersion` and output filename.

2. **Update the changelog.** In `CHANGELOG.md`, rename the `## [Unreleased]` heading's contents
   into a new dated section, then leave a fresh empty `## [Unreleased]` heading above it:

   ```markdown
   ## [Unreleased]

   ## [0.2.0] - 2026-10-15

   ### Added
   ...
   ```

   The heading **must** be exactly `## [X.Y.Z]` (matching `Directory.Build.props`'s `<Version>`
   exactly) - `.github/workflows/release.yml` extracts everything between that heading and the
   next `## [` heading and uses it as the GitHub Release notes verbatim.

3. **Open a PR** with the version bump and changelog update (title like
   `chore(release): bump version to 0.2.0`), get it reviewed, and merge it to `main` the normal
   way. Confirm the `build-installer` CI job on `main` after merging is green and its artifact
   looks right (size, contents) before tagging.

4. **Tag and push.**

   ```powershell
   git checkout main
   git pull
   git tag v0.2.0
   git push origin v0.2.0
   ```

   Pushing the tag triggers `.github/workflows/release.yml`, which:
   - Verifies the tag's version matches `Directory.Build.props`'s `<Version>` (fails loudly if
     someone forgot step 1, or tagged the wrong commit).
   - Builds, tests, publishes the self-contained single-file win-x64 build, and compiles the
     installer.
   - Computes the installer's SHA-256 checksum.
   - Creates a GitHub Release named `PC Manager <version>` with the setup exe, its `.sha256` file,
     and the matching `CHANGELOG.md` section as the release notes.

5. **Verify the release** on GitHub: download the setup exe, confirm its SHA-256 matches the
   published `.sha256` file, and (ideally, on a real or virtual Windows PC - never the machine
   used to develop PC Manager) run it through a fresh install and uninstall.

## Notes

- Code signing is out of scope for now - every installer will trigger a Windows SmartScreen
  warning ("Windows protected your PC" > **More info** > **Run anyway**). This is documented in
  the README's "Install" section for end users.
- If a release build or the installer compile fails, nothing is published - fix the issue, delete
  the bad local tag (`git tag -d vX.Y.Z`) if you already created one, and start again from step 4
  once `main` is fixed.
- The Inno Setup version used in CI (`choco install innosetup --version=...`) is pinned in both
  `.github/workflows/ci.yml` and `.github/workflows/release.yml` - bump both together if you need a
  newer Inno Setup.
