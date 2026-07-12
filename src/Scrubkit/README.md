# Scrubkit

**Point it at a folder — get back a clean table of file text + metadata, fully offline.**

Scrubkit walks a directory, pulls text and metadata out of common file types, and
scrubs common sensitive values (emails, phone numbers, card- and SSN-like numbers,
IPs) as it goes. You keep *what matters about each file* without carrying the raw
values downstream — ideal for RAG ingestion, search indexing, and on-device data prep.

---

## Highlights

- **Offline** — no network calls, no telemetry.
- **Small, fast core** — PDF, Office, text, and image metadata out of the box.
- **Built-in scrubbing** — best-effort redaction of common sensitive values.
- **Pluggable** — add formats with `IFileExtractor`, swap redaction with `IRedactor`.
- **Scales** — stream results and process files in parallel for large trees.
- **Multi-target** — `net8.0` and `netstandard2.0`.

---

## Install

```
dotnet add package Scrubkit
```

## Quick start

```csharp
using Scrubkit;

var scrubber = new FolderScrubber(new ReadOptions
{
    Recursion = Recursion.AllNested,
    Redaction = RedactionLevel.Standard,
});

IReadOnlyList<FileRecord> table = await scrubber.ReadAsync(@"C:\Docs");

foreach (var r in table)
    Console.WriteLine($"{r.Name} — {r.TypeBucket} — {r.Text.Length} chars");
```

Each `FileRecord` gives you the file's `Text`, `Metadata`, `TypeBucket`, a per-category
`Redactions` count, and any `Warnings`.

---

## Supported formats

| Category      | Extensions                                  |
| ------------- | ------------------------------------------- |
| Documents     | PDF, DOCX, RTF                              |
| Spreadsheets  | XLSX, CSV                                   |
| Presentations | PPTX                                        |
| Text          | TXT, MD, LOG, JSON, XML, HTML               |
| Images        | EXIF metadata (make / model / software)     |

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

Output order is preserved. Above a degree of 1, custom extractors and redactors should
be thread-safe (the built-ins are).

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

For different redaction, implement `IRedactor` and set `ReadOptions.Redactor`.

Shipping an add-on as its own package? Reference **`Scrubkit.Abstractions`** (contracts
only) to stay lightweight.

---

## A note on scrubbing

The built-in redactor is best-effort pattern matching. It reduces incidental exposure
of common sensitive values, but it will miss things and is not a guarantee. For stronger
redaction, plug in your own `IRedactor`.

*100% offline — no network calls, no telemetry.*
