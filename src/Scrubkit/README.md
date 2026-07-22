# Scrubkit

![Scrubkit — offline text + metadata extraction for .NET](https://raw.githubusercontent.com/jjopensoftworks-blip/Scrubkit/main/assets/banner.png)

[![CI](https://github.com/jjopensoftworks-blip/Scrubkit/actions/workflows/ci.yml/badge.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Scrubkit.svg)](https://www.nuget.org/packages/Scrubkit)
[![Downloads](https://img.shields.io/nuget/dt/Scrubkit.svg)](https://www.nuget.org/packages/Scrubkit)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

**[🌐 Website](https://jjopensoftworks-blip.github.io/Scrubkit/)** · **[📦 NuGet](https://www.nuget.org/packages/Scrubkit)** · **[📝 Changelog](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/CHANGELOG.md)**

**Point it at a folder — get back a clean table of file text + metadata, fully offline.**

Scrubkit walks a directory and pulls text and metadata out of common file types, returning
one row per file. Everything runs locally — no network calls, no telemetry — so it's a
natural first step for RAG ingestion, search indexing, and on-device data prep.

![Scrubkit playground output — a table of extracted files with type, size and text length](https://raw.githubusercontent.com/jjopensoftworks-blip/Scrubkit/main/assets/playground.png)

---

## Highlights

- **Offline** — no network calls, no telemetry.
- **Small, fast core** — PDF, Office, text, and image metadata out of the box.
- **Scrub PII & secrets** — opt-in redaction of emails, cards, IPs, and secrets (API keys,
  JWTs, private keys, connection strings).
- **RAG-ready** — a `Chunker` for overlapping windows and CSV / JSON / **JSON Lines** / Parquet output.
- **Incremental** — re-scan only what changed via a lightweight `Manifest` (`ReadChangesAsync`).
- **Runs from a shell** — the [`scrubkit`](https://www.nuget.org/packages/Scrubkit.Tool) CLI (`dotnet tool`) scans a folder with zero code.
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

| Category      | Extensions                                       |
| ------------- | ------------------------------------------------ |
| Documents     | PDF, DOCX                                        |
| Spreadsheets  | XLSX, CSV                                        |
| Presentations | PPTX                                             |
| Text          | TXT, MD, LOG, JSON, XML, HTML, HTM, RTF          |
| Images        | JPG, PNG, TIFF, HEIC, WebP, GIF, BMP (EXIF only) |

Text-family formats are read as raw text — markup in RTF/HTML/XML is **not** stripped.
Images yield **EXIF metadata only** (make / model / software) — no pixels, no OCR.
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

## Incremental: re-scan only what changed

Keep a lightweight `Manifest` (size + last-write time per file) and, on the next run, extract
only the added or modified files — unchanged ones are never re-read:

```csharp
var scrubber = new FolderScrubber();
var baseline = File.Exists("state.txt")
    ? Manifest.Load(new StreamReader("state.txt"))
    : Manifest.Empty;

IncrementalResult result = await scrubber.ReadChangesAsync(@"C:\Docs", baseline);

foreach (var doc in result.Changed) Index(doc);        // added + modified — (re)embed these
foreach (var gone in result.Removed) index.Delete(gone); // drop deleted files downstream

using var w = new StreamWriter("state.txt");
result.Manifest.Save(w);                                // persist for next time
```

Change detection is size + last-write time (no re-read), so a touched-but-identical file counts
as modified. The manifest is a zero-dependency text sidecar.

---

## Recipe: prepare a folder for RAG

Stream a document folder straight into a vector store — text + metadata ready to embed,
rows with no text skipped:

```csharp
var scrubber = new FolderScrubber(new ReadOptions
{
    MaxTextLength = 8_000,   // keep chunks index-friendly
});

var chunker = new Chunker(new ChunkOptions { MaxChars = 1_000, OverlapChars = 100 });

await foreach (var doc in scrubber.ReadStreamAsync(@"C:\Docs"))
{
    if (doc.Text.Length == 0) continue;   // skip metadata-only rows

    foreach (var chunk in chunker.Chunk(doc))   // overlapping windows, carrying path + metadata
        await index.UpsertAsync(
            id: $"{chunk.Path}#{chunk.Index}",
            text: chunk.Text,                    // ready to embed
            metadata: chunk.Metadata);
}
```

The `Chunker` splits each file's text into overlapping windows (whitespace-snapped by default so
words aren't cut), and `TableWriter.ToJsonLines(chunker.Chunk(table))` writes them straight to
**JSON Lines** for a batch embedding pipeline.

---

## Redact PII & secrets

Redaction is opt-in — set a `Redaction` level (or supply your own `IRedactor`). The built-in
`StandardRedactor` masks emails, phones, Luhn-checked cards, SSNs, IPs, IBANs, and **secrets in
recognisable formats** — PEM private keys, JWTs, credentialed connection strings, and AWS /
Google / GitHub / Slack keys. **Aggressive** additionally catches `key = value` credential
assignments and high-entropy tokens.

```csharp
var scrubber = new FolderScrubber(new ReadOptions { Redaction = RedactionLevel.Standard });
// "AWS key AKIAIOSFODNN7EXAMPLE" → "AWS key [API_KEY]"
```

Add your own **custom rules** (matched before the built-ins) plus allow/deny/disable lists via
`StandardRedactorOptions` — or from a JSON file with the CLI's `--rules`:

```csharp
var options = new StandardRedactorOptions { Level = RedactionLevel.Standard };
options.CustomRules.Add(new CustomRedactionRule { Category = "EmployeeId", Pattern = @"\bE\d{6}\b", Token = "[EMP]" });
var scrubber = new FolderScrubber(new ReadOptions { Redactor = new StandardRedactor(options) });
```

---

## Export the table

Serialize the records to CSV, JSON, or JSON Lines with the zero-dependency `TableWriter` (or
to Parquet via the separate **`Scrubkit.Parquet`** package):

```csharp
string csv   = TableWriter.ToCsv(table);
string json  = TableWriter.ToJson(table);
string jsonl = TableWriter.ToJsonLines(table);   // one record per line — RAG / log pipelines
```

Timestamps (`Modified`) are written in **UTC** with a trailing `Z`. Need machine-local time
instead? Pass `utc: false` — the value is emitted with an explicit offset (e.g.
`2026-07-19T15:30:00+05:30`) so it stays unambiguous:

```csharp
string localJson = TableWriter.ToJson(table, utc: false);
string localCsv  = TableWriter.ToCsv(table, utc: false);
```

`FileRecord.Modified` is itself always `DateTimeKind.Utc`, so you can equally convert
consumer-side with `record.Modified.ToLocalTime()`. (Parquet always stores UTC — a Parquet
timestamp is an instant, so convert on read if you want a local view.)

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

## Privacy & disclaimer

**Private by design** — 100% offline, no network calls, no telemetry; your files never leave
your machine (enforced by an offline-guarantee test).

**Best-effort, not a guarantee** — redaction is opt-in, best-effort pattern matching that
*will* miss things; it is **not a compliance tool**. Provided as-is under the MPL-2.0, no
warranty.

*100% offline — no network calls, no telemetry.*
