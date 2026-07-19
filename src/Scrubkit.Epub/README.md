# Scrubkit.Epub

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Epub.svg)](https://www.nuget.org/packages/Scrubkit.Epub)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

An add-on [`IFileExtractor`](https://www.nuget.org/packages/Scrubkit.Abstractions) for
[**Scrubkit**](https://www.nuget.org/packages/Scrubkit) that reads **`.epub`** e-books —
fully offline, with **no dependencies beyond `Scrubkit.Abstractions`**.

- **Body text** — the book's XHTML spine, in reading order, with tags stripped and HTML
  entities decoded.
- **Metadata** — `Title` / `Author` / `Subject` from the OPF package metadata.

An EPUB is just a zip of XHTML, so this reads it with the BCL — the same technique the
built-in Office and OpenDocument extractors use — and pulls in nothing extra.

## Install

```sh
dotnet add package Scrubkit.Epub
```

## Use it

Register the extractor via `ReadOptions.Extractors`. Registered extractors are tried before
the built-ins, so `.epub` files are routed here:

```csharp
using Scrubkit;

var options = new ReadOptions();
options.Extractors.Add(new EpubExtractor());

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
