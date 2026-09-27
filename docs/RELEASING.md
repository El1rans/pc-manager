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
   - Verifies the tagged commit is an ancestor of `origin/main` (fails if you tagged a commit that
     was never merged).
   - Verifies the tag's version matches `Directory.Build.props`'s `<Version>` (fails loudly if
     someone forgot step 1, or tagged the wrong commit).
   - Builds, tests, publishes the self-contained single-file win-x64 build, and compiles the
     installer.
   - Computes the installer's SHA-256 checksum.
   - Verifies the matching `CHANGELOG.md` section is non-empty (fails if step 2 was skipped or left
     the section blank).
   - Creates a GitHub Release named `PC Manager <version>` with the setup exe, its `.sha256` file,
     and that `CHANGELOG.md` section as the release notes.

5. **Verify the release** on GitHub: download the setup exe, confirm its SHA-256 matches the
   published `.sha256` file, and (ideally, on a real or virtual Windows PC - never the machine
   used to develop PC Manager) run it through a fresh install and uninstall.

## Code signing (SignPath)

PC Manager signs releases through the [SignPath Foundation](https://signpath.org/) program for
open source projects, once the application below is approved. Until then, `release.yml` publishes
unsigned installers automatically (see "Notes" below) - nothing here blocks a release.

### One-time setup (maintainer)

1. Apply at <https://signpath.org/apply> with this repository's URL. A draft of the application
   answers is at `docs/signing/APPLICATION.md` - review and adjust before submitting.
2. Enable multi-factor authentication on both the GitHub account used for this repository and the
   SignPath account - SignPath requires MFA before it will trust a GitHub Actions build.
3. Once SignPath approves the application:
   - Create a SignPath project named `pc-manager` (or set the `SIGNPATH_PROJECT_SLUG` repository
     variable to whatever slug is chosen).
   - Link the GitHub repository as this project's trusted build system (GitHub Actions), scoped to
     `.github/workflows/release.yml` so only tag-triggered release runs can submit signing
     requests, never a pull request build.
   - Add two artifact configurations, pasting the XML from this repo:
     - `app`, using `docs/signing/app.xml` (signs `PCManager.exe` inside the zip the workflow
       uploads).
     - `installer`, using `docs/signing/installer.xml` (signs the compiled
       `PCManager-Setup-<version>.exe`).
   - Create a signing policy named `release-signing` (or set `SIGNPATH_SIGNING_POLICY_SLUG`) with
     **approval required** - every signing request needs a manual click in SignPath before it is
     signed, even though the workflow submits it automatically.
   - Create an API token with the **Submitter** role (not Approver - approval is a separate manual
     step done in the SignPath UI by a human, deliberately not automatable from CI).
4. In this GitHub repository's settings, add:
   - Repository **variable** `SIGNPATH_ORGANIZATION_ID` - the organization id SignPath shows in
     its project settings.
   - Repository **secret** `SIGNPATH_API_TOKEN` - the submitter API token from the previous step.
   - Optionally, variables `SIGNPATH_PROJECT_SLUG` (defaults to `pc-manager` if unset) and
     `SIGNPATH_SIGNING_POLICY_SLUG` (defaults to `release-signing` if unset).

### Per release

Signing adds one manual step to the process in "Steps" above: after pushing the tag, `release.yml`
submits both the published `PCManager.exe` and the compiled installer to SignPath and waits for
them to be signed. **Someone with Approver access must open SignPath and approve each signing
request** (there are two per release: `app`, then `installer`) or the workflow will eventually time
out waiting. Once approved, the workflow downloads the signed files, verifies the installer's
Authenticode signature itself, computes the SHA-256 checksum from the *signed* installer, and
publishes the release as before.

### Uninstaller signing limitation

The installer's embedded uninstaller (`unins000.exe`) cannot be signed through this flow - see
"Uninstaller signing limitation" in `docs/CODE_SIGNING_POLICY.md` for why. It stays unsigned even
after `PCManager.exe` and the installer itself are signed.

## Notes

- Until the SignPath application above is approved and configured, every installer stays unsigned
  and will trigger a Windows SmartScreen warning ("Windows protected your PC" > **More info** >
  **Run anyway**). This is documented in the README's "Install" section for end users.
- If a release build or the installer compile fails, nothing is published - fix the issue, delete
  the bad tag both locally and on the remote (`git tag -d vX.Y.Z` and
  `git push --delete origin vX.Y.Z`) so a re-push of the same tag name triggers the workflow again,
  and start again from step 4 once `main` is fixed.
- The Inno Setup version used in CI (`choco install innosetup --version=...`) is pinned in both
  `.github/workflows/ci.yml` and `.github/workflows/release.yml` - bump both together if you need a
  newer Inno Setup.
