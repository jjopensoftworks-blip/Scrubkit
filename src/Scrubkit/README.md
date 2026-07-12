# Scrubkit

Point it at a folder, get back a clean table of **file text + metadata** — fully
offline. It quietly scrubs common sensitive values (emails, phones, card/SSN-like
numbers, IPs) so you keep *what matters about a file* without carrying the raw
personal data downstream.

The **core is fast and small** — it handles the common formats out of the box. Need
more? Add opt-in packages (e.g. `Scrubkit.Email`) that plug new extractors in.

```csharp
using Scrubkit;

var scrubber = new FolderScrubber(new ReadOptions
{
    Recursion       = Recursion.AllNested,   // or TopOnly
    MaxFiles        = 1000,
    MaxBytesPerFile = 25 * 1024 * 1024,
    Redaction       = RedactionLevel.Standard,
});

IReadOnlyList<FileRecord> table = await scrubber.ReadAsync(@"C:\Docs");

foreach (var r in table)
    Console.WriteLine($"{r.Name}\t{r.TypeBucket}\t{r.Text.Length} chars"
                    + (r.HasSensitiveData ? $"\t(scrubbed: {string.Join(",", r.Redactions.Keys)})" : ""));
```

## Core formats

PDF · DOCX · PPTX · XLSX (shared strings) · TXT/MD/CSV/LOG/JSON/XML/HTML/RTF ·
image EXIF (make/model/software). Unknown types return a metadata-only row.
Extraction never throws to the caller — problems land in `FileRecord.Warnings`.

## Adding formats

Implement `IFileExtractor` and register it — it's tried before the built-ins:

```csharp
public sealed class MyExtractor : IFileExtractor
{
    public bool CanHandle(string ext) => ext == ".xyz";
    public ExtractedContent Extract(string path) => new(metadata, text);
}

options.Extractors.Add(new MyExtractor());
```

Add-on packages (planned): `Scrubkit.Email` (.eml/.msg), OCR for scanned images,
EPUB, legacy Office, archives.

## Scrubbing is best-effort, not a guarantee

The built-in `StandardRedactor` is pattern matching. It reduces incidental exposure
of common personal data; **it will miss things and is not a compliance tool** (not a
substitute for a DLP or PHI review). Need stronger guarantees? Implement `IRedactor`
(e.g. an NER/model-based pass) and set `ReadOptions.Redactor`.

100% offline. No network calls. No telemetry.
