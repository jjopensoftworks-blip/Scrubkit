# Scrubkit.Email

[![NuGet](https://img.shields.io/nuget/v/Scrubkit.Email.svg)](https://www.nuget.org/packages/Scrubkit.Email)
[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://github.com/jjopensoftworks-blip/Scrubkit/blob/main/LICENSE)

Add-on [`IFileExtractor`](https://www.nuget.org/packages/Scrubkit.Abstractions)s for
[**Scrubkit**](https://www.nuget.org/packages/Scrubkit) that read **`.eml`** (MIME email)
and **`.msg`** (Outlook / OLE2) files — fully offline, with **no dependencies beyond
`Scrubkit.Abstractions`**.

- **Fields → metadata** — `From`, `To`, `Cc`, `Subject`, `Date`.
- **Body → text** — for `.eml`, prefers the `text/plain` part, falls back to `text/html`;
  for `.msg`, reads the message body property.
- **Understands real email** — `.eml`: multipart messages, `base64` and `quoted-printable`
  transfer encodings, common charsets, and RFC 2047 encoded-word headers. `.msg`: a built-in
  compound-file reader that pulls the MAPI property streams (Unicode, with ANSI fallback).
- **Skips noise** — attachments and non-text parts are ignored.

## Install

```sh
dotnet add package Scrubkit.Email
```

## Use it

Register the extractors via `ReadOptions.Extractors`. Registered extractors are tried before
the built-ins, so `.eml` and `.msg` files are routed here:

```csharp
using Scrubkit;

var options = new ReadOptions();
options.Extractors.Add(new EmailExtractor());   // .eml
options.Extractors.Add(new MsgExtractor());     // .msg

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
