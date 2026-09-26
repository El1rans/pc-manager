# Contributing

## Branching

- `main` is always releasable. Nobody commits to `main` directly; every change lands through a
  pull request.
- Branch names: `feat/<short-name>`, `fix/<short-name>`, `chore/<short-name>`, `docs/<short-name>`.

## Commit style

Commits follow [Conventional Commits](https://www.conventionalcommits.org/):
`feat(updates): ...`, `fix(dashboard): ...`, `test(core): ...`, `chore(ci): ...`. Keep commits
small and focused; each one should build.

## Building, running, and testing

Prerequisites: .NET SDK matching `global.json` (Windows, since the app uses WPF).

```powershell
dotnet build -c Release
dotnet test -c Release
dotnet run -c Release --project src/PCManager.App
```

Before opening or updating a pull request:

```powershell
git fetch origin
git rebase origin/main
dotnet build -c Release
dotnet test -c Release
```

Both must pass with zero warnings and zero errors before requesting review.

## Pull requests

- Use the PR template: summary, how it was tested, screenshots for UI changes, and a checklist of
  the spec's acceptance criteria.
- PRs are squash-merged after review; the branch is deleted after merge.
- CI (`.github/workflows/ci.yml`) must be green before merge.

## Code organization

See `docs/specs/00-engineering-standards.md` for the full solution layout and code quality gates.
In short: testable logic lives in `PCManager.Core` behind an interface; `PCManager.App` holds
views, view models, and DI wiring. A feature adds itself via its own folders, one DI registration
extension method, and one navigation entry, without touching other features' files.
