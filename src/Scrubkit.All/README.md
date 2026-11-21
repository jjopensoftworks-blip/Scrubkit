# Scrubkit.All

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.All.svg)](https://www.nuget.org/packages/Scrubkit.All)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

A convenience **meta-package** referencing the entire
[Scrubkit](https://github.com/jjopensoftworks-blip/Scrubkit) library family and providing the one-line
**`.AddAllExtractors()`** fluent extension on `ReadOptions`.

```sh
dotnet add package Scrubkit.All
```

## Quick start

```csharp
using Scrubkit;

var options = new ReadOptions
{
    Redaction = RedactionLevel.Standard,
    ComputeContentHash = true
}.AddAllExtractors(); // Registers PDF, Image EXIF, Email (.eml/.msg), OpenDocument, EPUB & Legacy Office

var scrubber = new FolderScrubber(options);
IReadOnlyList<FileRecord> table = await scrubber.ReadAsync(@"C:\Docs");
```

## What you get

| Package | What it adds |
| --- | --- |
| **Scrubkit** | Zero-dependency core: `FolderScrubber`, modern Office (`.docx`/`.xlsx`/`.pptx`), HTML, RTF, and plain text extractors, `StandardRedactor` (PII + secret scrubbing), `Chunker`, `Manifest`, and `TableWriter` (CSV / JSON / JSONL). |
| **Scrubkit.Pdf** | `.pdf` text & metadata extraction (`.AddPdf()`). |
| **Scrubkit.Images** | Image EXIF metadata extraction (`.AddImages()`). |
| **Scrubkit.Email** | Zero-dep `.eml` and `.msg` email extraction (`.AddEmail()`). |
| **Scrubkit.OpenDocument** | Zero-dep OpenDocument `.odt` / `.ods` / `.odp` extraction (`.AddOpenDocument()`). |
| **Scrubkit.Epub** | Zero-dep `.epub` e-book extraction (`.AddEpub()`). |
| **Scrubkit.LegacyOffice** | Zero-dep pre-2007 Office `.doc` / `.xls` / `.ppt` extraction (`.AddLegacyOffice()`). |
| **Scrubkit.Extensions.DependencyInjection** | `services.AddScrubkit(…)` + `ILogger` bridge for ASP.NET Core and worker hosts. |
| **Scrubkit.Extensions.MicrosoftExtensionsAI** | `IChatClient` and `IEmbeddingGenerator` local redaction middleware (`.UseRedaction(…)`). |
| **Scrubkit.Extensions.SemanticKernel** | `ISemanticTextMemory` and `IVectorStoreRecordCollection` folder ingestion extensions (`ImportFolderAsync`, `SaveChunksAsync`, `UpsertChunksAsync`). |
| **Scrubkit.Parquet** *(net8.0/net10.0)* | Apache Parquet columnar output (`ParquetTableWriter`). |

`Scrubkit.Abstractions` (the contracts) comes in transitively.

## When *not* to use it

If you want a minimal dependency footprint, reference only the packages you need — e.g. `Scrubkit` core alone has **zero third-party dependencies** and reads modern Office, HTML, RTF, and text out of the box, while each add-on is opt-in. On **`netstandard2.0`**, this bundle includes every library except **`Scrubkit.Parquet`**, which targets `net8.0` and `net10.0`.

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE). Fully offline — no network calls, no telemetry.
