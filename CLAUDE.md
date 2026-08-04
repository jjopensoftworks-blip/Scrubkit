# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Scrubkit is a multi-package .NET library that walks a folder, extracts text + metadata from
common file types, and can scrub common PII, returning a table of `FileRecord`s. Everything
runs offline — no network calls. Packages, grouped by role:

- **`Scrubkit.Abstractions`** — contracts only (`IFileExtractor`, `IRedactor`, `FileRecord`,
  `ReadOptions`, `ExtractedContent`, `RedactionResult`/`RedactionSpan`, `RedactionCategories`,
  `ScrubDiagnostic`, `Buckets`, enums). No heavy deps. **Add-ons reference only this.**
- **`Scrubkit`** — the core: `FolderScrubber`, built-in extractors, `StandardRedactor`,
  `TableWriter` (CSV/JSON). Depends on Abstractions + **PdfPig** (PDF) + **MetadataExtractor**
  (image EXIF).
- **Extractor add-ons** (each references only Abstractions, zero heavy deps): `Scrubkit.Email`
  (`.eml/.msg`), `Scrubkit.OpenDocument` (`.odt/.ods/.odp`), `Scrubkit.Epub` (`.epub`),
  `Scrubkit.LegacyOffice` (binary `.doc/.xls/.ppt`). All read their formats with the BCL
  (`System.IO.Compression` + `XDocument`/regex; OLE2/CFBF + record parsing for `.msg` and
  legacy Office).
- **Integration:** `Scrubkit.Extensions.DependencyInjection` — `services.AddScrubkit(...)`;
  references the core + `Microsoft.Extensions.DependencyInjection.Abstractions` (+ Logging).
- **AI Integration:** `Scrubkit.Extensions.MicrosoftExtensionsAI` — local PII/secret redaction
  middleware for `Microsoft.Extensions.AI` (`IChatClient` and `IEmbeddingGenerator`); multi-targets
  `netstandard2.0;net8.0;net10.0`.
- **Output:** `Scrubkit.Parquet` — Parquet output via Parquet.Net; multi-targets `net8.0;net10.0` (the rest
  multi-target `netstandard2.0;net8.0;net10.0`). CSV/JSON output stays zero-dep in the core.
- **Bundle:** `Scrubkit.All` — a **meta-package** with no code that references the whole family
  (core + all extractor add-ons + DI + Parquet) so consumers can install everything in one
  reference. `IncludeBuildOutput=false`, package validation off, `NoWarn=NU5128`; Parquet is
  referenced on modern .NET TFMs (net8.0/net10.0).

Everything stays in the flat `Scrubkit` namespace regardless of package or folder (including
the DI extension methods and the `AddScrubkit` entry point), so consuming code is identical.

The package family is deliberately grouped **by format family, not per-extension** (email is
one package, etc.) to avoid sprawl. New add-ons that need a heavy dependency get their own
package; zero-dep ones could too, but stay lightweight.

## Commands

```
dotnet build Scrubkit.slnx -c Release        # build all packages (all TFMs)
dotnet test  Scrubkit.slnx -c Release        # run the xUnit suite (~320 tests)
dotnet pack  Scrubkit.slnx -c Release -o artifacts   # produce .nupkg + .snupkg
dotnet run --project samples/Scrubkit.Playground     # runnable demo (synthetic-PII folder)
dotnet run -c Release --project benchmarks/Scrubkit.Benchmarks   # BenchmarkDotNet throughput
```

Run a single test: `dotnet test --filter "FullyQualifiedName~<name>"`.

The coverage gate CI enforces (99.5% line floor) is `build/coverage-gate.py`, run over the
Cobertura reports from `dotnet test --collect:"XPlat Code Coverage"`. `build/test-summary.py`
posts the per-run PR summary.

The solution is the modern XML format **`Scrubkit.slnx`**, not a classic `.sln`. The
`benchmarks/` project is deliberately **not** in the solution (keeps CI lean); run it directly.

## Architecture

The pipeline lives in [FolderScrubber.cs](src/Scrubkit/FolderScrubber.cs) and flows:
enumerate → pick extractor → extract → normalize whitespace → clip → (optional) redact text +
metadata → (optional) content hash → emit `FileRecord`, firing an optional per-file
diagnostic. It buffers via `ReadAsync` or streams via `ReadStreamAsync` (order-preserving,
bounded parallelism through a sliding task window). Two extension seams drive everything:

- **`IFileExtractor`** ([IFileExtractor.cs](src/Scrubkit.Abstractions/IFileExtractor.cs)) —
  one implementation per format. Core built-ins in [Extractors/](src/Scrubkit/Extractors/):
  PDF (PdfPig), Office OOXML, plain text, image EXIF. Add-ons live in their own packages and
  register via `ReadOptions.Extractors`.
- **`IRedactor`** ([IRedactor.cs](src/Scrubkit.Abstractions/IRedactor.cs)) — the swappable PII
  seam. Redaction is **opt-in**: nothing is redacted unless the caller supplies a `Redactor`
  or sets a `Redaction` level.

The **contracts** live in `src/Scrubkit.Abstractions/`; the **implementations** in
`src/Scrubkit/`. Keep that split — an add-on must be able to reference only Abstractions.

### The redaction engine ([StandardRedactor.cs](src/Scrubkit/StandardRedactor.cs))

`StandardRedactor` is a **single overlap-resolving pass**, not sequential replaces:

- A static `Rules` table lists regex patterns in **priority order** (email, Luhn-checked card,
  IBAN, SSN, MAC, IPv4, IPv6, phone, then Aggressive-only geo/DOB/long-number). Deny-list
  terms are claimed first.
- Each match tries to claim its span; overlaps with an already-claimed (higher-priority) span
  are rejected. Claimed characters are **masked** in a working copy so a looser later pattern
  can't reach into text a more specific one already took. This masking is load-bearing — a
  regression here silently drops valid redactions (there's a fuzz/property test guarding it).
- The result reports `RedactionResult.Spans` — offsets/lengths into the **original** text —
  plus per-category `Counts`. `StandardRedactorOptions` adds per-category disable, custom
  tokens, and allow/deny lists.

### Output, diagnostics, hashing

- **`TableWriter`** (core, zero-dep) serializes `FileRecord`s to CSV or JSON (JSON is
  hand-rolled with proper escaping — tests validate it by round-tripping through
  `System.Text.Json`). **`ParquetTableWriter`** (in `Scrubkit.Parquet`) does Parquet via
  `ParquetSerializer` over a flat row projection.
- **Diagnostics:** `ReadOptions.OnDiagnostic` is a dependency-free `Action<ScrubDiagnostic>`
  hook the core fires per file. The DI package's `AddScrubkit` bridges it to `ILogger` when a
  logger factory is present — so no logging dependency is forced on the core.
- **Content hash:** opt-in `ReadOptions.ComputeContentHash` → SHA-256 on
  `FileRecord.ContentHash`, bounded by `MaxBytesPerFile` (default **10 MB**; oversized files
  are stat-skipped and never read).

### Invariants to preserve when editing

- **Extraction never throws to the caller.** `FolderScrubber.ReadOne` isolates every per-file
  failure and records it in `FileRecord.Warnings` (`extract-failed`, `stat-failed`,
  `skipped-content`, `text-clipped`, `hash-failed`). Only `ReadAsync` throws, and only
  `DirectoryNotFoundException` for a missing root.
- **Registered extractors win over built-ins.** `_options.Extractors` are prepended before the
  built-ins and `ExtractorFor` takes the *first* `CanHandle` match — so an add-on can override
  a built-in. Don't reorder this.
- **Redaction: priority order + masking + Luhn.** Keep the `Rules` order (specific patterns
  first), the masking of claimed spans, and Luhn validation on cards. Spans are original-text
  offsets — don't switch them to redacted-text offsets.
- **Metadata is scrubbed too**, and its redaction counts fold into `FileRecord.Redactions`.
- Extensions are compared **lower-cased with the leading dot** (e.g. `".pdf"`); `CanHandle`
  receives them in that form. `Buckets.For` maps extension → coarse `TypeBucket`.

## Build infrastructure

- **`Directory.Build.props`** holds shared settings (nullable, langversion, analyzers,
  deterministic + SourceLink, shared package metadata, `TreatWarningsAsErrors` in Release).
  Don't duplicate these in `.csproj`.
- **Central Package Management**: all versions live in `Directory.Packages.props`; `.csproj`
  files reference packages **by name only**. Add a new dependency's version there.
- **Versioning is tag-driven via MinVer** — no `<Version>` in any `.csproj`. Pushing tag
  `vX.Y.Z` produces package `X.Y.Z`; between tags builds are `X.Y.(Z+1)-alpha.0.<height>`.
- **Package validation** is on (`PackageValidationBaselineVersion` in `Directory.Build.props`,
  bumped to each stable release). A **brand-new** package must override it to empty in its
  `.csproj` until its first version is published, then it inherits the shared baseline.
- **netstandard2.0 support** relies on **PolySharp** (polyfills for records / `required` /
  `init` / ranges) plus [Polyfills.cs](src/Scrubkit/Polyfills.cs) (`GetValueOrDefault` shim).
  Avoid APIs missing on netstandard2.0.
- CI is [ci.yml](.github/workflows/ci.yml) (build/test/coverage-gate/pack on push+PR);
  publishing is [release.yml](.github/workflows/release.yml) (on `v*` tag, pushes all produced
  `.nupkg`s to NuGet via Trusted Publishing/OIDC).

## Conventions

- **Namespace is flat `Scrubkit`** for every file regardless of package or folder — including
  the DI extension methods (so `AddScrubkit` requires `using Scrubkit;`).
- Nullable and ImplicitUsings are enabled; `LangVersion` is `latest`.
- The public API is documented with XML doc comments (`///`) that double as the NuGet API
  docs — keep them accurate when changing signatures.
- Scrubbing is explicitly **best-effort, not a compliance tool**; don't overstate its
  guarantees in docs or comments.
- **Test convention:** an add-on/integration test project that references the core (or a heavy
  third-party lib) to prove end-to-end routing **omits `coverlet.collector`** — otherwise its
  partial view of that assembly reports as 0% and drags the solution-wide coverage gate. Such
  tests still run in CI; they just don't emit a coverage report.
- **Workflow (STRICT):**
  - **NEVER commit directly to `main` or merge locally into `main`.**
  - Always create a fresh branch (`feature/<name>`, `fix/<name>`, or `chore/<name>`) off `main`.
  - Push the branch to remote `origin` (`git push -u origin <branch-name>`) and open a **GitHub Pull Request**.
  - Merge via GitHub PR. Only tag releases (`vX.Y.Z`) on `main` after the PR is merged on GitHub.

## Development & Git Workflow (STRICT FOR ALL AI ASSISTANTS)

> [!IMPORTANT]
> **Strict PR Policy:**
> - **NEVER commit directly to `main` or merge branches locally into `main`.**
> - **One branch per PR:** Create a fresh branch off `main` for every change (`feature/<name>`, `fix/<name>`, or `chore/<name>`).
> - **Always push feature branches to remote:** `git push -u origin <branch-name>`.
> - **Always merge via GitHub Pull Request:** All code changes land on `main` exclusively through GitHub PRs.
> - **Release & Post-Release Housekeeping Procedure:**
>   1. Implement feature/fix on a fresh branch → Push to `origin` → Open Pull Request.
>   2. After PR is merged on GitHub → Switch to `main` and pull: `git checkout main && git pull origin main`.
>   3. Tag release: `git tag vX.Y.Z && git push origin vX.Y.Z` (triggers GitHub Actions release workflow to publish NuGet packages).
>   4. Post-release housekeeping:
>      - **Baseline bump:** Create `chore/baseline-X.Y.Z` → Update `PackageValidationBaselineVersion` in `Directory.Build.props` → Push to `origin` → Open PR.
>      - **Web docs update:** Create `docs/update-vX.Y.Z` → Update `docs/index.html` and `docs/changelog.html` → Push to `origin` → Open PR.
>      - **Branch cleanup:** Delete stale local and remote feature/release branches after PR merge (`git branch -d <branch>`, `git push origin --delete <branch>`).
