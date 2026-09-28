# Releasing Porchlight

Every push to a PR or `main` already builds and compiles the installer (see `.github/workflows/ci.yml`,
job `build-installer`) and uploads `Porchlight-Setup-<version>.exe` as a workflow artifact, so you can
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

   Pushing the tag triggers `.github/workflows/release.yml`, which runs as two jobs:

   `build-sign` (permissions: `contents: read`, `actions: read`; runs in the `release`
   [environment](#the-release-environment)):
   - Verifies the tagged commit is an ancestor of `origin/main` (fails if you tagged a commit that
     was never merged).
   - Verifies the tag's version matches `Directory.Build.props`'s `<Version>` (fails loudly if
     someone forgot step 1, or tagged the wrong commit).
   - Builds, tests, publishes the self-contained single-file win-x64 build, and compiles the
     installer, signing both through SignPath once it's configured (see "Code signing (SignPath)"
     below).
   - Computes the (signed, if applicable) installer's SHA-256 checksum.
   - Verifies the matching `CHANGELOG.md` section is non-empty (fails if step 2 was skipped or left
     the section blank), and builds the release notes from it, adding a visible "This release is
     unsigned." line when unsigned and always linking the
     [code signing policy](CODE_SIGNING_POLICY.md).
   - Uploads the installer, its checksum and the release notes as a workflow artifact for the next
     job.

   `publish` (needs `build-sign`; permissions: `contents: write` - the only job that can create a
   release; does not touch SignPath credentials at all):
   - Downloads that artifact and creates a GitHub Release named `Porchlight <version>` with the
     setup exe, its `.sha256` file, and the prepared release notes.

5. **Verify the release** on GitHub: download the setup exe, confirm its SHA-256 matches the
   published `.sha256` file, and (ideally, on a real or virtual Windows PC - never the machine
   used to develop Porchlight) run it through a fresh install and uninstall.

## Code signing (SignPath)

Porchlight signs releases through the [SignPath Foundation](https://signpath.org/) program for
open source projects, once the application below is approved. Until then, `release.yml` publishes
unsigned installers automatically (see "Notes" below) - nothing here blocks a release.

### Step 0: cut one unsigned release first

SignPath Foundation's program requires an applicant project to already have at least one public
release before it will review the application - so before applying, go through the "Steps" section
above once with signing not yet configured (it publishes unsigned automatically) to produce a first
tagged GitHub Release. Only then move on to the one-time setup below.

### The `release` environment

`build-sign` runs in a GitHub Actions **environment** named `release`, which is where
`SIGNPATH_API_TOKEN` lives as an **environment secret** (rather than a plain repository secret) -
this is what lets a deployment rule restrict exactly when that token can be used. Create it once,
in this repository's Settings -> Environments:

- Name: `release`.
- **Deployment branches and tags**: restrict to tags matching `v*.*.*` - the same pattern this
  workflow already triggers on, so the token is never reachable from a branch build, only from an
  actual release tag push.
- Add the environment secret `SIGNPATH_API_TOKEN` here (see step 4 below), not as a repository
  secret.

### One-time setup (maintainer)

1. Apply at <https://signpath.org/apply> with this repository's URL. A draft of the application
   answers is at `docs/signing/APPLICATION.md` - review and adjust before submitting. (See "Step 0"
   above - do this only after a first unsigned release exists.)
2. Enable multi-factor authentication on both the GitHub account used for this repository and the
   SignPath account - SignPath requires MFA before it will trust a GitHub Actions build.
3. Once SignPath approves the application:
   - Create a SignPath project named `porchlight` (or set the `SIGNPATH_PROJECT_SLUG` repository
     variable to whatever slug is chosen).
   - Link the GitHub repository as this project's trusted build system (GitHub Actions), scoped to
     `.github/workflows/release.yml` so only tag-triggered release runs can submit signing
     requests, never a pull request build. Optionally, also turn on SignPath's
     [GitHub build policies](https://docs.signpath.io/trusted-build-systems/github) -
     `runners.require_github_hosted: true` (refuse self-hosted runners) and
     `disallow_reruns: true` (refuse re-running an already-completed job) - both tighten what
     SignPath will accept as a "trusted" build for this project, beyond what the workflow-path
     scoping above already restricts.
   - Add two artifact configurations, pasting the XML from this repo:
     - `app`, using `docs/signing/app.xml` (signs `Porchlight.exe` inside the zip the workflow
       uploads).
     - `installer`, using `docs/signing/installer.xml` (signs the compiled
       `Porchlight-Setup-<version>.exe` inside the zip the workflow uploads).
     Both configurations declare a required `version` parameter (SignPath Foundation requires
     artifact metadata restrictions) and set `product-name="Porchlight"` /
     `product-version="${version}"` on their `<pe-file>` - the workflow passes this parameter on
     every signing request from `Directory.Build.props`'s `<Version>`.
   - Create a signing policy named `release-signing` (or set `SIGNPATH_SIGNING_POLICY_SLUG`) with
     **approval required** - every signing request needs a manual click in SignPath before it is
     signed, even though the workflow submits it automatically.
   - Create an API token with the **Submitter** role (not Approver - approval is a separate manual
     step done in the SignPath UI by a human, deliberately not automatable from CI).
4. Set up the [`release` environment](#the-release-environment) above, then in it add:
   - Environment **secret** `SIGNPATH_API_TOKEN` - the submitter API token from the previous step.
5. In this GitHub repository's settings (plain repository variables, not environment-scoped - these
   aren't secret), add:
   - Repository **variable** `SIGNPATH_ORGANIZATION_ID` - the organization id SignPath shows in
     its project settings.
   - Optionally, variables `SIGNPATH_PROJECT_SLUG` (defaults to `porchlight` if unset),
     `SIGNPATH_SIGNING_POLICY_SLUG` (defaults to `release-signing` if unset), and
     `SIGNPATH_REQUIRED` (set to `true` to make `build-sign` **fail** a release outright if signing
     isn't configured, instead of the default of silently publishing unsigned - flip this on once
     SignPath is expected to always be available).

### Per release

Signing adds one manual step to the process in "Steps" above: after pushing the tag, `build-sign`
submits both the published `Porchlight.exe` and the compiled installer to SignPath and waits for
them to be signed. **Someone with Approver access must open SignPath and approve each signing
request** (there are two per release: `app`, then `installer`) or the job will eventually time out
waiting. Once approved, the job downloads the signed files, verifies both Porchlight.exe's and the
installer's Authenticode signatures itself (including that each carries a trusted timestamp, so the
signature keeps validating after the signing certificate itself expires), computes the SHA-256
checksum from the *signed* installer, and hands the signed installer, checksum and release notes to
the `publish` job, which creates the release as before.

### Uninstaller signing limitation

The installer's embedded uninstaller (`unins000.exe`) cannot be signed through this flow - see
"Uninstaller signing limitation" in `docs/CODE_SIGNING_POLICY.md` for why. It stays unsigned even
after `Porchlight.exe` and the installer itself are signed.

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
