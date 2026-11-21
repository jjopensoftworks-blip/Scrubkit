# Scrubkit.OpenDocument

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.OpenDocument.svg)](https://www.nuget.org/packages/Scrubkit.OpenDocument)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

An add-on [`IFileExtractor`](https://www.nuget.org/packages/Scrubkit.Abstractions) for
[**Scrubkit**](https://www.nuget.org/packages/Scrubkit) that reads **OpenDocument Format**
files from LibreOffice / OpenOffice — fully offline, with **no dependencies beyond
`Scrubkit.Abstractions`**. Multi-targets `net8.0`, `net10.0`, and `netstandard2.0`.

- **`.odt`** — text documents
- **`.ods`** — spreadsheets
- **`.odp`** — presentations

Body text becomes `Text`; the `Title` / `Author` / `Subject` document properties become
`Metadata`, with zero external dependencies.

## Install

```sh
dotnet add package Scrubkit.OpenDocument
```

## Use it

Register the extractor with `.AddOpenDocument()` (or manually via `ReadOptions.Extractors`):

```csharp
using Scrubkit;

var options = new ReadOptions()
    .AddOpenDocument(); // Registers OpenDocumentExtractor (.odt, .ods, .odp)

var scrubber = new FolderScrubber(options);

foreach (var r in await scrubber.ReadAsync(@"C:\Docs"))
    Console.WriteLine($"{r.Name} — {r.TypeBucket} — {r.Text.Length} chars");
```

`.odt` comes back as a `Document` row, `.ods` as `Spreadsheet`, and `.odp` as
`Presentation`.

## Scope

Extracts text and the core document properties — **best-effort**, consistent with
Scrubkit's other extractors. Formatting, styles, and embedded objects are not reproduced,
and it never throws to the batch (per-file problems surface as `Warnings` on the row).

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE).
