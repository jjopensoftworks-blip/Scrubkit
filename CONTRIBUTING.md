# Contributing to Scrubkit

Thanks for your interest! This guide gets you productive quickly and explains the few
conventions that keep the codebase clean.

By participating you agree to the [Code of Conduct](CODE_OF_CONDUCT.md).

## Getting started

Requires the .NET SDK (8.0 or newer — see [`global.json`](global.json)).

```bash
dotnet build Scrubkit.slnx -c Release     # build all packages (netstandard2.0 + net8.0)
dotnet test  Scrubkit.slnx -c Release     # run the test suite
dotnet run --project samples/Scrubkit.Playground   # try it on a generated demo folder
```

## Repository layout

```
src/Scrubkit.Abstractions   contracts only — no heavy dependencies
src/Scrubkit                the core: FolderScrubber, built-in extractors, StandardRedactor
tests/Scrubkit.Tests        xUnit tests
samples/Scrubkit.Playground runnable demo
```

## Conventions

- **Keep the Abstractions boundary.** `IFileExtractor`, `IRedactor`, `FileRecord`,
  `ReadOptions`, and friends live in `Scrubkit.Abstractions`, which must stay
  **dependency-free**. Never add PdfPig/MetadataExtractor (or other heavy deps) there —
  they belong in `Scrubkit` (core) or in an add-on package. Everything stays in the flat
  `Scrubkit` namespace.
- **Release builds are strict.** `TreatWarningsAsErrors` is on for Release (what CI runs),
  so fix warnings. Style is enforced by [`.editorconfig`](.editorconfig) (file-scoped
  namespaces, etc.).
- **Document public API** with XML doc comments — they ship as the NuGet API docs.
- **Extraction never throws to the caller.** An `IFileExtractor.Extract` *may* throw;
  `FolderScrubber` isolates per-file failures into `FileRecord.Warnings`. Preserve that.
- **Thread-safety:** extractors/redactors may run concurrently when
  `ReadOptions.MaxDegreeOfParallelism > 1`, so keep them stateless/thread-safe.

## Adding a dependency

Versions are centralized (Central Package Management) and locked:

1. Add a `<PackageVersion>` to [`Directory.Packages.props`](Directory.Packages.props).
2. Reference it (no `Version=`) in the relevant `.csproj`.
3. Run `dotnet restore` to update the `packages.lock.json` files, and **commit them** —
   CI restores with `--locked-mode` and will fail on a drifted lock.

## Extending Scrubkit

- **New format** — implement `IFileExtractor` (referencing only `Scrubkit.Abstractions`)
  and register it via `ReadOptions.Extractors`. Registered extractors are tried *before*
  the built-ins, so you can override one. See the package README for a worked example.
- **Stronger scrubbing** — implement `IRedactor` and set `ReadOptions.Redactor`.

Please add tests for new extractors/redactors. Prefer synthesizing fixtures in-test (see
`OfficeExtractorTests` / `PdfExtractorTests`) over committing binaries.

## Tests & coverage

`dotnet test -c Release` must be green. CI also enforces a **line-coverage floor** via
[`build/coverage-gate.py`](build/coverage-gate.py), so cover new code.

## Pull requests

- Branch off `main`; keep PRs focused.
- Ensure `dotnet build -c Release` and `dotnet test -c Release` pass locally.
- Describe the change and link any related issue.

## Releasing (maintainers)

Versions come from Git tags via MinVer — nothing to bump in a file. Tag `main` and push:

```bash
git tag v1.0.0 && git push origin v1.0.0
```

The release workflow builds, tests, packs, and publishes both packages to NuGet via
Trusted Publishing (OIDC). A one-time nuget.org policy + `NUGET_USER` repo variable are
required first.
