# Scrubkit.Email

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Email.svg)](https://www.nuget.org/packages/Scrubkit.Email)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

Add-on [`IFileExtractor`](https://www.nuget.org/packages/Scrubkit.Abstractions)s for
[**Scrubkit**](https://www.nuget.org/packages/Scrubkit) that read **`.eml`**
and **`.msg`** (Outlook) email files — fully offline, with **no dependencies beyond
`Scrubkit.Abstractions`**. Multi-targets `net8.0`, `net10.0`, and `netstandard2.0`.

- **Fields → metadata** — `From`, `To`, `Cc`, `Subject`, `Date`.
- **Body → text** — extracts clean readable text from message bodies.
- **Skips noise** — attachments and non-text parts are ignored.

## Install

```sh
dotnet add package Scrubkit.Email
```

## Use it

Register both `.eml` and `.msg` extractors with `.AddEmail()` (or manually via `ReadOptions.Extractors`):

```csharp
using Scrubkit;

var options = new ReadOptions()
    .AddEmail(); // Registers EmailExtractor (.eml) and MsgExtractor (.msg)

var scrubber = new FolderScrubber(options);

foreach (var r in await scrubber.ReadAsync(@"C:\Mail"))
    Console.WriteLine($"{r.Name} — {r.Metadata.GetValueOrDefault("Subject")} — {r.Text.Length} chars");
```

Each email row comes back with the message's `Subject`/`From`/… in `Metadata` and the
body in `Text`, ready for indexing or redaction (supply an `IRedactor` if you want the
body and headers scrubbed).

## Scope

Reads **`.eml`** and **`.msg`**. Parsing is **best-effort**, consistent with Scrubkit's
other extractors — it favors resilience over strict conformance and never throws to the
batch (per-file problems surface as `Warnings` on the row). For `.msg` the body and the
`From`/`To`/`Cc`/`Subject`/`Date` properties are read; attachments and embedded objects are
skipped.

## License

[Mozilla Public License 2.0](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE).
