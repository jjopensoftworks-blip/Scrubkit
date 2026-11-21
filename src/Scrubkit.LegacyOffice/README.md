# Scrubkit.LegacyOffice

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.LegacyOffice.svg)](https://www.nuget.org/packages/Scrubkit.LegacyOffice)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

An add-on [`IFileExtractor`](https://www.nuget.org/packages/Scrubkit.Abstractions) for
[**Scrubkit**](https://www.nuget.org/packages/Scrubkit) that reads the **pre-2007**
Microsoft Office formats — Word **`.doc`**, Excel **`.xls`**, and PowerPoint **`.ppt`** —
fully offline, with **no dependencies beyond `Scrubkit.Abstractions`**. Multi-targets `net8.0`, `net10.0`, and `netstandard2.0`.

- **Body → text** — Word paragraphs, Excel cell strings, PowerPoint slide/notes text.
- **Properties → metadata** — `Title`, `Author`, `Subject` document properties.
- **Zero-dependency** — no third-party libraries or Office interop required.

> The **modern** OOXML formats (`.docx`/`.xlsx`/`.pptx`) are handled by the Scrubkit core —
> this package is only for the old binary formats.

## Install

```sh
dotnet add package Scrubkit.LegacyOffice
```

## Use it

Register the extractor with `.AddLegacyOffice()` (or manually via `ReadOptions.Extractors`):

```csharp
using Scrubkit;

var options = new ReadOptions()
    .AddLegacyOffice(); // Registers LegacyOfficeExtractor (.doc, .xls, .ppt)

var scrubber = new FolderScrubber(options);

foreach (var r in await scrubber.ReadAsync(@"C:\Docs"))
    Console.WriteLine($"{r.Name} — {r.Metadata.GetValueOrDefault("Title")} — {r.Text.Length} chars");
```

## Scope

Reads **`.doc` / `.xls` / `.ppt`** (Office 97-2003). Parsing is **best-effort**, consistent
with Scrubkit's other extractors — it favors resilience over full-fidelity reproduction and
never throws to the batch (per-file problems surface as `Warnings` on the row). Rich
formatting, embedded objects, and encrypted files are out of scope; the goal is clean text
plus the core document properties.

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE).
