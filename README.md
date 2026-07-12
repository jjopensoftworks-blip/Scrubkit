# Scrubkit

[![CI](https://github.com/jjopensoftworks-blip/Scrubkit/actions/workflows/ci.yml/badge.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Scrubkit.svg)](https://www.nuget.org/packages/Scrubkit)
[![Downloads](https://img.shields.io/nuget/dt/Scrubkit.svg)](https://www.nuget.org/packages/Scrubkit)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](LICENSE)

**Point at a folder, get a clean table of file text + metadata back — fully offline.**
Scrubkit walks a directory, extracts text and metadata from common file types, and
quietly scrubs common sensitive values (emails, phones, card/SSN-like numbers, IPs)
so you keep *what matters about a file* without carrying the raw personal data
downstream. Ideal for RAG ingestion, search indexing, and data-prep pipelines that
must stay on-device.

- **Offline.** No network calls, no telemetry — everything runs locally.
- **Fast, small core.** PDF · Office (docx/pptx/xlsx) · text · image EXIF, on
  PdfPig + MetadataExtractor only.
- **Pluggable.** Add formats via `IFileExtractor`; opt-in add-on packages
  (`Scrubkit.Email`, OCR, …) keep heavy dependencies out of the core.
- **Best-effort scrubbing.** Swap in your own `IRedactor` for stronger guarantees.

## Install

```
dotnet add package Scrubkit
```

## Quick start

```csharp
using Scrubkit;

var scrubber = new FolderScrubber(new ReadOptions { Recursion = Recursion.AllNested });
IReadOnlyList<FileRecord> table = await scrubber.ReadAsync(@"C:\Docs");
```

See [`src/Scrubkit/README.md`](src/Scrubkit/README.md) for the full API.

## Try it without installing

The playground creates a demo folder full of fake PII and scrubs it, so you can see
the output before adding the package to your project:

```
dotnet run --project samples/Scrubkit.Playground
```

Point it at a real folder (nothing leaves your machine):

```
dotnet run --project samples/Scrubkit.Playground -- "C:\Docs" --level aggressive
```

## Build & test

```
dotnet build -c Release        # builds all packages (netstandard2.0 + net8.0)
dotnet test  -c Release        # runs the test suite
```

## Repository layout

```
src/Scrubkit.Abstractions   contracts only (IFileExtractor, IRedactor, …) — no heavy deps
src/Scrubkit                the core: FolderScrubber + built-in extractors + StandardRedactor
tests/Scrubkit.Tests        xUnit tests
samples/Scrubkit.Playground runnable demo
```

Package versions come from Git tags via [MinVer](https://github.com/adamralph/minver)
(e.g. tag `v0.1.0` → package `0.1.0`); dependency versions are centralized in
`Directory.Packages.props`.

## A note on scrubbing

The built-in redactor is best-effort pattern matching. It reduces incidental
exposure of common personal data; **it is not a compliance tool** and is not a
substitute for a DLP/PHI review. For stronger guarantees, implement `IRedactor`.

## Contributing

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) and the
[Code of Conduct](CODE_OF_CONDUCT.md). Changes are tracked in [CHANGELOG.md](CHANGELOG.md).

## License

[Mozilla Public License 2.0](LICENSE) — open and free to use, including in
closed/commercial apps; modifications to Scrubkit's own source files stay open.
