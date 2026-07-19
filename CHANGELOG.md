# Changelog

<!--
One section per release, newest first. Each header shows a Stable/Pre-release badge, the
tag, and the date, then changes grouped as Features / Bug Fixes / Maintenance so readers
can tell at a glance whether a release adds things or just fixes them. Versions are cut
from Git tags via MinVer.
-->

## 1.4.0

![Pre-release](https://img.shields.io/badge/release-Pre--release-e0a106?style=flat-square) &nbsp; 🏷️ `v1.4.0` &nbsp;·&nbsp; 📅 Unreleased

&nbsp;

---

A capability release: turn the scrubbed table into output, watch the run, and hash content.

### 🚀 Output: CSV / JSON and a new Parquet package

- **`TableWriter`** (in the core, zero-dependency) serializes the record table to **CSV** or
  **JSON** — `TableWriter.ToCsv(table)` / `ToJson(table)`. CSV is a flat, RFC 4180-quoted
  summary; JSON carries the full record including text, metadata, redaction counts, warnings,
  and content hash.
- **`Modified` timestamps are written in UTC** (trailing `Z`) by default. Pass **`utc: false`**
  to emit machine-local time instead — always with an explicit offset (e.g.
  `2026-07-19T15:30:00+05:30`) so the value stays unambiguous.
- **New package `Scrubkit.Parquet`** writes the table to Apache **Parquet** via Parquet.Net
  (`ParquetTableWriter`), for data-lake / analytics ingestion. **net8.0-only**; the core stays
  zero-dependency. Parquet always stores `Modified` as a UTC instant.

### 🚀 Diagnostics and an ILogger seam

- **`ReadOptions.OnDiagnostic`** is a dependency-free `Action<ScrubDiagnostic>` hook the core
  fires per file — a `read` event on success, or a warning code (`extract-failed`,
  `skipped-content`, `text-clipped`, `stat-failed`, `hash-failed`) on a problem.
- **`AddScrubkit`** bridges that hook to **`ILogger`** when a logger factory is present, so the
  core takes no logging dependency.

### 🚀 Content hashing

- Opt-in **`ReadOptions.ComputeContentHash`** populates **`FileRecord.ContentHash`** with the
  file's lower-case hex **SHA-256**, bounded by `MaxBytesPerFile` (oversized files are
  stat-skipped and never read).

### 🔧 Changes

- **Default `MaxBytesPerFile` lowered to 10 MB.** Files over the limit are stat-skipped with a
  `skipped-content` warning rather than read into memory.
- **`FileRecord.Modified` is now UTC** (`DateTimeKind.Utc`) — the canonical, unambiguous form;
  convert consumer-side with `Modified.ToLocalTime()` for a local view.

### 🐛 Bug fixes

- **Redaction no longer drops a valid match that overlaps a higher-priority span.** The
  single-pass engine now masks claimed characters in a working copy, so a looser later pattern
  can't reach into text a more specific one already took.

### 🧰 Maintenance

- CI coverage floor raised to **99%** (actual line coverage ~99.8%).
- Added a **Privacy & disclaimer** section to the site and package READMEs.

## 1.3.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.3.0` &nbsp;·&nbsp; 📅 2026-07-19

&nbsp;

---

Two more add-on packages, a DI integration, and a published throughput number.

### 🚀 New package: Scrubkit.Epub

- **`Scrubkit.Epub`** reads **`.epub`** e-books: the OPF `Title` / `Author` / `Subject`
  become metadata and the XHTML spine (tags stripped, entities decoded) becomes text, in
  reading order. Zero dependencies beyond `Scrubkit.Abstractions`; `.epub` files come back
  as `Document` rows.

### 🚀 New package: Scrubkit.Extensions.DependencyInjection

- **`services.AddScrubkit(…)`** registers a configured `FolderScrubber` as a singleton for
  ASP.NET Core, worker services, and other generic hosts. Configure recursion, limits,
  add-on extractors, and an optional redactor via a `ReadOptions` hook (with an
  `IServiceProvider` overload for DI-resolved dependencies).

### 🏎️ Benchmarks

- Added a **BenchmarkDotNet** throughput harness (`benchmarks/`). A representative run
  extracts on the order of **~12,000 files/sec** (4-way parallel; ~4,900 sequential) over a
  mixed text-file corpus.

### 🐛 Bug fixes

- **`.epub` files now report `TypeBucket = "Document"`.** `Buckets.For` didn't map the
  extension, so `.epub` files extracted fine but bucketed as `Other`.

## 1.2.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.2.0` &nbsp;·&nbsp; 📅 2026-07-18

&nbsp;

---

Introduces the first **add-on packages**, exercising the `IFileExtractor` extension seam
end to end. Both reference only `Scrubkit.Abstractions`, so they pull in no PDF or image
libraries; register either via `ReadOptions.Extractors`.

### 🚀 New package: Scrubkit.Email

- **`Scrubkit.Email`** ships an `EmailExtractor` that reads **`.eml`** (MIME) files: the
  `From` / `To` / `Cc` / `Subject` / `Date` headers become metadata and the message body
  becomes text. It handles multipart messages, `base64` and `quoted-printable` transfer
  encodings, common charsets, and RFC 2047 encoded-word headers — preferring the
  `text/plain` part and falling back to `text/html`. Attachments are skipped. `.eml` files
  come back as `TypeBucket = "Email"` rows.

### 🚀 New package: Scrubkit.OpenDocument

- **`Scrubkit.OpenDocument`** ships an `OpenDocumentExtractor` that reads OpenDocument
  Format files from LibreOffice / OpenOffice — text documents (**`.odt`**), spreadsheets
  (**`.ods`**), and presentations (**`.odp`**). Body text becomes `Text` and the
  `Title` / `Author` / `Subject` properties become metadata. ODF is a zip of XML, read with
  the BCL — the same technique the built-in Office extractor uses. Files route to their
  natural buckets (`Document` / `Spreadsheet` / `Presentation`).

## 1.1.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.1.0` &nbsp;·&nbsp; 📅 2026-07-17

&nbsp;

---

Repositioned the package around **offline text + metadata extraction**. Redaction is now
**opt-in** instead of applied by default.

### ⚠️ Behavior change

- **Redaction is opt-in and caller-driven.** The core extracts and returns text + metadata
  exactly as read; it redacts only when you supply an `IRedactor` via `ReadOptions.Redactor`
  (or set a `Redaction` level). **1.0.0 redacted by default** — if you relied on that, set a
  redactor or level explicitly after upgrading.

### 📖 Positioning & docs

- Repositioned `Scrubkit` as **offline text + metadata extraction for .NET**. Updated the
  package `Title`, `Description`, tags, and both READMEs to lead with extraction.
- Refreshed the playground into a neutral extraction demo with an **extraction-view**
  screenshot (synthetic sample data only).
- Clarified that text-family formats (RTF / HTML / XML) are read as raw text.

### 🧪 Tests & tooling

- Expanded the test suite — extraction, extension filtering, record fields, opt-in and
  custom redaction, and edge cases (empty folders, uncapped limits, corrupt Office files,
  and a `stat-failed` guard).
- Added an **offline-guarantee test** that fails the build if either shipping assembly ever
  references a networking assembly.
- Added a **netstandard2.0 runtime test project** that runs the `netstandard2.0` build (its
  PolySharp polyfills and `GetValueOrDefault` shim) on the .NET 8 host, so the polyfilled
  paths are exercised at runtime — not just compiled.
- CI now publishes a **test-status summary** on every run.
- Tidied package author/copyright metadata and optimized the package icon.

### 🐛 Bug fixes

- **`.htm` files now report `TypeBucket = "Text"`.** `Buckets.For` mapped `.html` but not
  `.htm`, so `.htm` files extracted correctly yet were bucketed `Other`.


## 1.0.0

![Stable](https://img.shields.io/badge/release-Stable-2ea44f?style=flat-square) &nbsp; 🏷️ `v1.0.0` &nbsp;·&nbsp; 📅 2026-07-15

&nbsp;

---

Promoted from the preview to a **stable** release. Same engine and API as the preview —
**no new features and no bug fixes** — with a cleaner first impression and clearer, more
accurate wording throughout. The public API is stable under SemVer from here on.

### 📖 Documentation

- Redesigned the **NuGet package page** (README): a scannable highlights list, separate
  Install and Quick-start sections, a supported-formats table, and clear section dividers
  with more breathing room.
- Shortened the quick-start sample so long lines no longer cause a horizontal scrollbar on
  the package page.
- Redesigned the **`Scrubkit.Abstractions`** page with a focused example and a
  "what's inside" contract list.
- Added a **"Prepare a folder for RAG"** recipe to the package and project READMEs.
- Folded a short extension guide (writing a custom `IFileExtractor` or `IRedactor`) into the
  package README.

### 🔎 Discoverability & packaging

- Rewrote the package **description** to lead with the problem it solves — offline text +
  metadata extraction and scrubbing for RAG ingestion, search indexing, and data prep — and
  the formats it reads.
- Refreshed the **search tags** (added `text-extraction` and `office`; removed noise) so the
  right people can find it.

### 🛡️ Under the hood

- Enabled **package validation** — the build now checks that the `net8.0` and
  `netstandard2.0` API surfaces stay compatible within each package.

### ✍️ Wording & accuracy

- Removed references to add-on packages that aren't shipped yet, so the docs describe only
  what's available today.
- Reworded the scrubbing docs and code comments to plain, neutral language and softened
  absolute claims — the redactor is clearly described as **best-effort**.
- Tidied the playground output and XML doc comments to match.

### 🐛 Bug fixes

- _None — no behavior changed since the preview._

&nbsp;

## 0.1.0-preview.1

![Pre-release](https://img.shields.io/badge/release-Pre--release-d29922?style=flat-square) &nbsp; 🏷️ `v0.1.0-preview.1` &nbsp;·&nbsp; 📅 2026-07-13

&nbsp;

---

The first public release — everything Scrubkit does today shipped here.

### 🚀 Features

- **Point it at a folder, get a clean table back.** `FolderScrubber` walks a directory and
  returns one row per file — its text, metadata, and a tally of what was scrubbed — fully
  offline. Unreadable files never crash the run; problems show up as warnings instead.
- **Reads the common formats out of the box** — PDF, Word / Excel / PowerPoint
  (docx / xlsx / pptx), plain-text files (txt, md, csv, log, json, xml, html), and the
  camera/software info stored in image EXIF.
- **Scrubs common sensitive values** — emails, phone numbers, card numbers (validated with
  the Luhn check to cut false hits), SSNs, and IP addresses. An *Aggressive* mode also
  catches date-of-birth-like dates and long digit runs.
- **Handles big folders** — stream results as they're produced (`ReadStreamAsync`) and
  process several files at once (`MaxDegreeOfParallelism`), with output order preserved.
- **Extend it without forking** — plug in your own formats (`IFileExtractor`) or your own
  redaction (`IRedactor`); add-ons only need the tiny `Scrubkit.Abstractions` package.

### 🛠️ Under the hood

- Runs on **.NET 8** and **.NET Standard 2.0**.
- Reproducible, source-linked builds with symbol packages; automated tests and publishing.

### 🐛 Bug fixes

- _None — first release._
