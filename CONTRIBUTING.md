# Contributing to Scrubkit

Thanks for your interest! This guide gets you productive quickly and explains the few
conventions that keep the codebase clean.

By participating you agree to the [Code of Conduct](CODE_OF_CONDUCT.md).

## Getting started

Requires the .NET SDK (see [`global.json`](global.json)).

```bash
dotnet build Scrubkit.slnx -c Release     # build all 14 packages (netstandard2.0 + net8.0 + net10.0)
dotnet test  Scrubkit.slnx -c Release     # run the xUnit suite (575+ tests across net8.0/net10.0)
dotnet run --project samples/Scrubkit.Playground         # try it on a generated demo folder
dotnet run --project samples/Scrubkit.Sample.RagIngestion # RAG ingestion + chunking + manifest demo
```

## Repository layout

```text
src/Scrubkit.Abstractions                     contracts only — zero dependencies
src/Scrubkit                                  zero-dep core: FolderScrubber, Office/HTML/RTF/Text extractors, StandardRedactor, Chunker, Manifest, TableWriter
src/Scrubkit.Pdf                              PDF text + metadata extractor via PdfPig (.AddPdf())
src/Scrubkit.Images                           Image EXIF metadata extractor via MetadataExtractor (.AddImages())
src/Scrubkit.Email                            Zero-dep .eml and .msg email extractors (.AddEmail())
src/Scrubkit.OpenDocument                     Zero-dep .odt/.ods/.odp OpenDocument extractor (.AddOpenDocument())
src/Scrubkit.Epub                             Zero-dep .epub eBook extractor (.AddEpub())
src/Scrubkit.LegacyOffice                     Zero-dep binary .doc/.xls/.ppt OLE2 extractor (.AddLegacyOffice())
src/Scrubkit.Extensions.DependencyInjection   services.AddScrubkit(...) + ILogger diagnostic bridge
src/Scrubkit.Extensions.MicrosoftExtensionsAI IChatClient & IEmbeddingGenerator local redaction middleware
src/Scrubkit.Extensions.SemanticKernel        ISemanticTextMemory & IVectorStoreRecordCollection ingestion
src/Scrubkit.Parquet                          Apache Parquet columnar output via Parquet.Net
src/Scrubkit.All                              Meta-bundle referencing all libraries + .AddAllExtractors()
src/Scrubkit.Tool                             Global CLI dotnet tool (`scrubkit`)
tests/                                        xUnit test projects (plus Scrubkit.Tests.NetStd20 contract check)
samples/                                      Runnable Playground and RagIngestion sample apps
benchmarks/                                   BenchmarkDotNet throughput suite (run on demand)
```

## Conventions

- **Keep the Abstractions and zero-dep Core boundaries.** `IFileExtractor`, `IRedactor`, `FileRecord`,
  `ReadOptions`, and friends live in `Scrubkit.Abstractions`, which must stay
  **dependency-free**. The core `Scrubkit` package also has **zero third-party dependencies**.
  Third-party dependencies belong strictly in dedicated add-on packages (`Scrubkit.Pdf`, `Scrubkit.Images`, `Scrubkit.Parquet`).
  Everything stays in the flat `Scrubkit` namespace.
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
  and register it via `ReadOptions.Extractors` or a fluent `.Add*()` extension. Registered
  extractors are tried *before* the built-ins, so you can override one.
- **Stronger scrubbing** — implement `IRedactor` and set `ReadOptions.Redactor`.

Please add tests for new extractors/redactors. Prefer synthesizing fixtures in-test (see
`OfficeExtractorTests` / `PdfExtractorTests`) over committing binaries.

## Tests & coverage

`dotnet test -c Release` must be green. CI also enforces a **99.5% line-coverage floor** via
[`build/coverage-gate.py`](build/coverage-gate.py), so cover new code.

## Pull requests

- Always create a fresh branch off `main` (`feature/<name>`, `fix/<name>`, or `chore/<name>`); never commit directly to `main`.
- Ensure `dotnet build Scrubkit.slnx -c Release` and `dotnet test Scrubkit.slnx -c Release` pass locally.
- Describe the change and link any related issue.

## Releasing (maintainers)

Versions come from Git tags via MinVer — nothing to bump in a file. After a PR merges into `main`, pull `main`, tag, and push:

```bash
git tag v2.2.0 && git push origin v2.2.0
```

The release workflow builds, tests, packs, and publishes all 14 packages to NuGet via
Trusted Publishing (OIDC). A one-time nuget.org policy + `NUGET_USER` repo variable are
required first.
