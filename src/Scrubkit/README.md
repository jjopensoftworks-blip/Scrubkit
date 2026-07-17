# Scrubkit

[![CI](https://github.com/jjopensoftworks-blip/Scrubkit/actions/workflows/ci.yml/badge.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Scrubkit.svg)](https://www.nuget.org/packages/Scrubkit)
[![Downloads](https://img.shields.io/nuget/dt/Scrubkit.svg)](https://www.nuget.org/packages/Scrubkit)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

**Point it at a folder — get back a clean table of file text + metadata, fully offline.**

Scrubkit walks a directory and pulls text and metadata out of common file types, returning
one row per file. Everything runs locally — no network calls, no telemetry — so it's a
natural first step for RAG ingestion, search indexing, and on-device data prep.

![Scrubkit playground output — a table of extracted files with type, size and text length](https://raw.githubusercontent.com/jjopensoftworks-blip/Scrubkit/main/assets/playground.png)

---

## Highlights

- **Offline** — no network calls, no telemetry.
- **Small, fast core** — PDF, Office, text, and image metadata out of the box.
- **Pluggable** — add or override formats with `IFileExtractor`.
- **Scales** — stream results and process files in parallel for large trees.
- **Multi-target** — `net8.0` and `netstandard2.0`.

---

## Install

```sh
dotnet add package Scrubkit
```

## Quick start

```csharp
using Scrubkit;

var scrubber = new FolderScrubber(new ReadOptions { Recursion = Recursion.AllNested });

IReadOnlyList<FileRecord> table = await scrubber.ReadAsync(@"C:\Docs");

foreach (var r in table)
    Console.WriteLine($"{r.Name} — {r.TypeBucket} — {r.Text.Length} chars");
```

Each `FileRecord` gives you the file's `Text`, `Metadata`, `TypeBucket`, `SizeBytes`,
`Modified`, and any `Warnings`.

---

## Supported formats

| Category      | Extensions                                  |
| ------------- | ------------------------------------------- |
| Documents     | PDF, DOCX                                   |
| Spreadsheets  | XLSX, CSV                                   |
| Presentations | PPTX                                        |
| Text          | TXT, MD, LOG, JSON, XML, HTML, RTF          |
| Images        | EXIF metadata (make / model / software)     |

Text-family formats are read as raw text — markup in RTF/HTML/XML is **not** stripped.
Unknown types return a metadata-only row. Extraction never throws to the caller —
per-file problems land in `FileRecord.Warnings`.

---

## Large folders: stream & parallelize

`ReadAsync` buffers the whole table. For big trees, stream records as they're produced
and process files concurrently:

```csharp
var options = new ReadOptions { MaxDegreeOfParallelism = 4 };

await foreach (var r in new FolderScrubber(options).ReadStreamAsync(@"C:\Docs"))
    Index(r);
```

Output order is preserved. Above a degree of 1, custom extractors should be thread-safe
(the built-ins are).

---

## Recipe: prepare a folder for RAG

Stream a document folder straight into a vector store — text + metadata ready to embed,
rows with no text skipped:

```csharp
var scrubber = new FolderScrubber(new ReadOptions
{
    MaxTextLength = 8_000,   // keep chunks index-friendly
});

await foreach (var doc in scrubber.ReadStreamAsync(@"C:\Docs"))
{
    if (doc.Text.Length == 0) continue;   // skip metadata-only rows

    await index.UpsertAsync(
        id: doc.Path,
        text: doc.Text,                   // extracted text, ready to embed
        metadata: doc.Metadata);
}
```

---

## Extend it

Add a format by implementing `IFileExtractor` — it's tried before the built-ins, so you
can also override one:

```csharp
public sealed class MyExtractor : IFileExtractor
{
    public bool CanHandle(string ext) => ext == ".xyz";
    public ExtractedContent Extract(string path) => new(metadata, text);
}

options.Extractors.Add(new MyExtractor());
```

Extracted text is returned exactly as read. To transform it — redaction, masking,
normalization — supply an `IRedactor` via `ReadOptions.Redactor`; it's entirely opt-in and
your choice.

Shipping an add-on as its own package? Reference **`Scrubkit.Abstractions`** (contracts
only) to stay lightweight.

---

*100% offline — no network calls, no telemetry.*
