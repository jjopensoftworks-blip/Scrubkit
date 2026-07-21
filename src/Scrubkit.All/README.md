# Scrubkit.All

A convenience **meta-package** — it ships no code of its own, it just references the whole
[Scrubkit](https://github.com/jjopensoftworks-blip/Scrubkit) family so you can read
everything with a single reference.

```
dotnet add package Scrubkit.All
```

## What you get

| Package | What it adds |
| --- | --- |
| **Scrubkit** | Core: folder → text + metadata for PDF, Office, text, and image EXIF, opt-in PII redaction, and zero-dependency CSV / JSON output. |
| **Scrubkit.Email** | `.eml` (MIME) email. |
| **Scrubkit.OpenDocument** | OpenDocument `.odt` / `.ods` / `.odp`. |
| **Scrubkit.Epub** | `.epub` e-books. |
| **Scrubkit.Extensions.DependencyInjection** | `services.AddScrubkit(…)` for ASP.NET Core and worker hosts. |
| **Scrubkit.Parquet** *(net8.0 only)* | Apache Parquet output via Parquet.Net. |

`Scrubkit.Abstractions` (the contracts) comes in transitively.

## When *not* to use it

If you want a lean footprint, reference just the packages you need instead — e.g. the core
alone reads PDF/Office/text out of the box, and each add-on is opt-in. On **netstandard2.0**
this bundle includes everything except **Scrubkit.Parquet**, which is net8.0-only.

The add-on extractors register via `ReadOptions.Extractors`; see the core package's README
for usage.

## License

MPL-2.0. Fully offline — no network calls, no telemetry.
