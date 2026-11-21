# Scrubkit.Epub

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Epub.svg)](https://www.nuget.org/packages/Scrubkit.Epub)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

An add-on [`IFileExtractor`](https://www.nuget.org/packages/Scrubkit.Abstractions) for
[**Scrubkit**](https://www.nuget.org/packages/Scrubkit) that reads **`.epub`** e-books —
fully offline, with **no dependencies beyond `Scrubkit.Abstractions`**. Multi-targets `net8.0`, `net10.0`, and `netstandard2.0`.

- **Body text** — the book's chapters in reading order, stripped to clean text.
- **Metadata** — `Title` / `Author` / `Subject` from the book metadata.

## Install

```sh
dotnet add package Scrubkit.Epub
```

## Use it

Register the extractor with `.AddEpub()` (or manually via `ReadOptions.Extractors`):

```csharp
using Scrubkit;

var options = new ReadOptions()
    .AddEpub(); // Registers EpubExtractor (.epub)

var scrubber = new FolderScrubber(options);

foreach (var r in await scrubber.ReadAsync(@"C:\Books"))
    Console.WriteLine($"{r.Name} — {r.Metadata.GetValueOrDefault("Title")} — {r.Text.Length} chars");
```

`.epub` files come back as `Document` rows.

## Scope

Extracts readable text and the core metadata — **best-effort**, consistent with Scrubkit's
other extractors. Images, styling, and layout are not reproduced, and it never throws to the
batch (per-file problems surface as `Warnings` on the row).

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE).
