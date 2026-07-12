# Changelog

All notable changes to Scrubkit are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Versions are derived from Git tags by [MinVer](https://github.com/adamralph/minver);
the `[Unreleased]` section below becomes the first tagged release (`0.1.0`).

## [Unreleased]

### Added
- `Scrubkit.Abstractions` package — dependency-free contracts (`IFileExtractor`,
  `IRedactor`, `FileRecord`, `ReadOptions`, `ExtractedContent`, `RedactionResult`,
  `Buckets`, enums) so add-on authors don't pull in PdfPig or MetadataExtractor.
- `FolderScrubber.ReadStreamAsync(...)` — streams `IAsyncEnumerable<FileRecord>` so large
  trees can be processed without buffering the whole table.
- `ReadOptions.MaxDegreeOfParallelism` (default 1) — bounded, **order-preserving** parallel
  file processing.
- Built-in extractors for PDF (PdfPig), Office OOXML (docx/pptx/xlsx),
  plain text, and image EXIF; `StandardRedactor` (emails, phones, Luhn-checked cards,
  SSN, IPs, plus DOB/long-digit runs at `Aggressive`).
- `samples/Scrubkit.Playground` — a runnable, zero-setup demo.

### Packaging & infrastructure
- Multi-targets `netstandard2.0` and `net8.0` (PolySharp + `Microsoft.Bcl.AsyncInterfaces`
  for the netstandard2.0 build).
- Central Package Management, committed lock files, deterministic builds, SourceLink,
  symbol packages (`.snupkg`), package icon, and MinVer tag-driven versioning.
- CI (build/test/coverage-gate on a Linux+Windows matrix) and a tag-triggered release
  workflow publishing to NuGet via Trusted Publishing (OIDC).

[Unreleased]: https://github.com/jjopensoftworks-blip/Scrubkit/commits/main
